using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;


[BurstCompile]
public struct NewPortalNodeTraversalJob : IJob
{
    public int2 TargetIndex;
    public int FieldColAmount;
    public int FieldRowAmount;
    public float FieldTileSize;
    public int SectorColAmount;
    public int SectorMatrixColAmount;

    public NativeSlice<float2> SourcePositions;
    public NativeArray<PortalTraversalData> PortalTraversalDataArray;
    public NativeList<ActivePortal> PortalSequence;

    public NativeList<int> PortalSequenceBorders;
    public UnsafeList<int> SectorToPicked;
    public UnsafeList<PathSectorState> SectorStateTable;
    public NativeList<int> PickedToSector;
    public NativeArray<DijkstraTile> TargetSectorCosts;
    public NativeArray<int> FlowFieldLength;

    [ReadOnly] public UnsafeList<SectorNode> SectorNodes;
    [ReadOnly] public NativeArray<int> SecToWinPtrs;
    [ReadOnly] public NativeArray<WindowNode> WindowNodes;
    [ReadOnly] public NativeArray<int> WinToSecPtrs;
    [ReadOnly] public UnsafeList<PortalNode> PortalNodes;
    [ReadOnly] public NativeArray<PortalToPortal> PorPtrs;
    [ReadOnly] public UnsafeList<byte> Costs;
    [ReadOnly] public NativeArray<SectorDirectionData> LocalDirections;
    [ReadOnly] public UnsafeList<UnsafeList<int>> IslandFields;

    public NativeList<int> TargetNeighbourPortalIndicies;
    public NativeList<int> AStarTraverseIndexList;
    public NativeList<int> SourcePortalIndexList;
    public NativeQueue<int> FastMarchingQueue;

    int _targetSectorStartIndex1d;
    int _targetSectorIndex1d;
    public void Execute()
    {
        //TARGET DATA
        int2 targetSectorIndex2d = new int2(TargetIndex.x / SectorColAmount, TargetIndex.y / SectorColAmount);
        _targetSectorIndex1d = targetSectorIndex2d.y * SectorMatrixColAmount + targetSectorIndex2d.x;
        int2 _targetSectorStartIndex2d = targetSectorIndex2d * SectorColAmount;
        _targetSectorStartIndex1d = _targetSectorStartIndex2d.y * FieldColAmount + _targetSectorStartIndex2d.x;
        int targetGeneralIndex1d = TargetIndex.y * FieldColAmount + TargetIndex.x;

        TargetSectorCosts = GetIntegratedCosts(targetGeneralIndex1d);

        //SET TARGET PORTAL DATA
        PortalTraversalDataArray[PortalTraversalDataArray.Length - 1] = new PortalTraversalData()
        {
            OriginIndex = PortalTraversalDataArray.Length - 1,
            DistanceFromTarget = 0,
            HCost = float.MaxValue,
            GCost = float.MaxValue,
            FCost = float.MaxValue,
            Mark = PortalTraversalMark.AStarPicked,
        };

        //SET TARGET NEIGHBOUR DATA
        SetTargetNeighbourPortalDataAndAddToList();
        if(TargetNeighbourPortalIndicies.Length == 0)
        {
            AddTargetSector();
            return;
        }
        //START GRAPH WALKER
        PortalSequenceBorders.Add(0);
        UnsafeHeap<int> walkerHeap = new UnsafeHeap<int>(10, Allocator.Temp);
        
        UnsafeList<int> allSorucePortalIndicies = GetSourcePortalIndicies();

        for (int i = 0; i < allSorucePortalIndicies.Length; i++)
        {
            int stoppedIndex = RunReductionAStar(allSorucePortalIndicies[i], walkerHeap);
            if (stoppedIndex == -1) { continue; }
            PickAStarNodes(allSorucePortalIndicies[i], stoppedIndex);
            ResetTraversedIndicies();
            walkerHeap.Clear();
        }
        
        RunFastMarching();
        for(int  i = 0; i < allSorucePortalIndicies.Length; i++)
        {
            PickPortalSequenceFromFastMarching(allSorucePortalIndicies[i]);
        }
        PickSectorsFromPortalSequence();
        AddTargetSector();
    }
    void PickPortalSequenceFromFastMarching(int sourcePortal)
    {
        PortalTraversalData sourceData = PortalTraversalDataArray[sourcePortal];
        if (sourceData.HasMark(PortalTraversalMark.FastMarchPicked)) { return; }
        if(sourceData.NextIndex == -1) { return; }

        sourceData.Mark |= PortalTraversalMark.FastMarchPicked;
        PortalTraversalDataArray[sourcePortal] = sourceData;

        ActivePortal sourceActivePortal = new ActivePortal()
        {
            Index = sourcePortal,
            Distance = sourceData.DistanceFromTarget,
            NextIndex = sourceData.NextIndex,
        };
        PortalSequence.Add(sourceActivePortal);

        int curIndex = sourceData.NextIndex;

        while(curIndex != -1)
        {
            PortalTraversalData curData = PortalTraversalDataArray[curIndex];

            //PUSH ACTIVE PORTAL
            ActivePortal curActivePortal = new ActivePortal()
            {
                Index = curIndex,
                Distance = curData.DistanceFromTarget,
                NextIndex = curData.NextIndex,
            };
            PortalSequence.Add(curActivePortal);

            //MARK OR STOP
            if (curData.HasMark(PortalTraversalMark.FastMarchPicked)) { break; }
            curData.Mark |= PortalTraversalMark.FastMarchPicked;
            PortalTraversalDataArray[curIndex] = curData;

            curIndex = curData.NextIndex;
        }

        ActivePortal terminator = new ActivePortal();
        terminator.SetTerminator();
        PortalSequence.Add(terminator);
        PortalSequenceBorders.Add(PortalSequence.Length);
    }
    void ResetTraversedIndicies()
    {
        PortalTraversalMark bitsToSet = ~(PortalTraversalMark.Added | PortalTraversalMark.Extracted);
        for (int i = 0; i < AStarTraverseIndexList.Length; i++)
        {
            int index = AStarTraverseIndexList[i];
            PortalTraversalData travData = PortalTraversalDataArray[index];
            travData.Mark &= bitsToSet;
            PortalTraversalDataArray[index] = travData;
        }
        AStarTraverseIndexList.Clear();
    }
    void PickAStarNodes(int sourceNodeIndex, int stoppedIndex)
    {
        int originIndex = stoppedIndex;

        //FIRST STEP
        PortalTraversalData stoppedPortalData = PortalTraversalDataArray[stoppedIndex];
        stoppedPortalData.Mark |= PortalTraversalMark.AStarPicked;
        PortalTraversalDataArray[originIndex] = stoppedPortalData;

        originIndex = stoppedPortalData.OriginIndex;

        //REMAINING STEPS
        while (originIndex != sourceNodeIndex)
        {
            PortalTraversalData nextPortalData = PortalTraversalDataArray[originIndex];
            nextPortalData.Mark |= PortalTraversalMark.AStarPicked;
            PortalTraversalDataArray[originIndex] = nextPortalData;
            originIndex = nextPortalData.OriginIndex;
        }

        //LAST STEP
        PortalTraversalData sourcePortalData = PortalTraversalDataArray[originIndex];
        sourcePortalData.Mark |= PortalTraversalMark.AStarPicked;
        PortalTraversalDataArray[originIndex] = sourcePortalData;
    }
    UnsafeList<int> GetSourcePortalIndicies()
    {
        int targetIsland = GetIsland(TargetIndex);
        UnsafeList<int> indicies = new UnsafeList<int>(0, Allocator.Temp);
        int sectorTileAmount = SectorColAmount * SectorColAmount;
        for (int i = 0; i < SourcePositions.Length; i++)
        {
            float2 sourcePos = SourcePositions[i];
            int2 sourceIndex = new int2((int)math.floor(sourcePos.x / FieldTileSize), (int)math.floor(sourcePos.y / FieldTileSize));
            int2 sourceSectorIndex = sourceIndex / SectorColAmount;
            int sourceSectorIndexFlat = sourceSectorIndex.y * SectorMatrixColAmount + sourceSectorIndex.x;

            //ADD SOURCE SECTOR TO THE PICKED SECTORS
            if (SectorToPicked[sourceSectorIndexFlat] != 0) { continue; }
            SectorToPicked[sourceSectorIndexFlat] = PickedToSector.Length * sectorTileAmount + 1;
            PickedToSector.Add(sourceSectorIndexFlat);
            SectorStateTable[sourceSectorIndexFlat] = PathSectorState.Included;
            //ADD SOURCE SECTOR TO THE PICKED SECTORS
            SetSectorPortalIndicies(sourceSectorIndexFlat, SourcePortalIndexList);

            for (int j = 0; j < SourcePortalIndexList.Length; j++)
            {
                int index = SourcePortalIndexList[j];
                PortalTraversalData travData = PortalTraversalDataArray[index];
                if (travData.HasMark(PortalTraversalMark.Source)) { continue; }
                if (PortalNodes[index].IslandIndex != targetIsland) { continue; }
                travData.Mark |= PortalTraversalMark.Source;
                PortalTraversalDataArray[index] = travData;
                indicies.Add(index);
            }
        }
        return indicies;

    }
    void RunFastMarching()
    {
        NativeQueue<int> fastMarchingQueue = FastMarchingQueue;
        NativeArray<PortalTraversalData> portalTraversalDataArray = PortalTraversalDataArray;
        UnsafeList<PortalNode> portalNodes = PortalNodes;
        NativeArray<PortalToPortal> porPtrs = PorPtrs;

        //MARK TARGET NEIGHBOURS
        for(int i = 0; i< TargetNeighbourPortalIndicies.Length; i++)
        {
            int index = TargetNeighbourPortalIndicies[i];
            PortalTraversalData targetNeighbour = PortalTraversalDataArray[index];
            targetNeighbour.Mark |= PortalTraversalMark.FastMarchTraversed;
            PortalTraversalDataArray[index] = targetNeighbour;
        }
        
        for(int i = 0; i< TargetNeighbourPortalIndicies.Length; i++)
        {
            int index = TargetNeighbourPortalIndicies[i];
            EnqueueNeighbours(index);
        }

        while (!fastMarchingQueue.IsEmpty())
        {
            int curIndex = fastMarchingQueue.Dequeue();

            //GET BEST NEIGHBOUR
            PortalNode portal = portalNodes[curIndex];
            int por1Ptr = portal.Portal1.PorToPorPtr;
            int por1Cnt = portal.Portal1.PorToPorCnt;
            int por2Ptr = portal.Portal2.PorToPorPtr;
            int por2Cnt = portal.Portal2.PorToPorCnt;
            float minDistance = float.MaxValue;
            int indexWithMinDistance = -1;
            for (int i = por1Ptr; i < por1Ptr + por1Cnt; i++)
            {
                PortalToPortal porToPor = porPtrs[i];
                int portalIndex = porToPor.Index;
                PortalTraversalData porData = portalTraversalDataArray[portalIndex];
                float totalDistance = porData.DistanceFromTarget + porToPor.Distance + 1;
                indexWithMinDistance = math.select(indexWithMinDistance, portalIndex, totalDistance < minDistance);
                minDistance = math.select(minDistance, totalDistance, totalDistance < minDistance);
            }
            for (int i = por2Ptr; i < por2Ptr + por2Cnt; i++)
            {
                PortalToPortal porToPor = porPtrs[i];
                int portalIndex = porToPor.Index;
                PortalTraversalData porData = portalTraversalDataArray[portalIndex];
                float totalDistance = porData.DistanceFromTarget + porToPor.Distance + 1;
                indexWithMinDistance = math.select(indexWithMinDistance, portalIndex, totalDistance < minDistance);
                minDistance = math.select(minDistance, totalDistance, totalDistance < minDistance);
            }

            //APPLY COST AND ORIGIN
            PortalTraversalData curData = PortalTraversalDataArray[curIndex];
            curData.NextIndex = indexWithMinDistance;
            curData.DistanceFromTarget = minDistance;
            PortalTraversalDataArray[curIndex] = curData;

            //ENQUEUE NEIGHBOURS
            EnqueueNeighbours(curIndex);
        }

        void EnqueueNeighbours(int index)
        {
            PortalNode portal = portalNodes[index];
            int por1Ptr = portal.Portal1.PorToPorPtr;
            int por1Cnt = portal.Portal1.PorToPorCnt;
            int por2Ptr = portal.Portal2.PorToPorPtr;
            int por2Cnt = portal.Portal2.PorToPorCnt;

            for(int i = por1Ptr; i < por1Ptr + por1Cnt; i++)
            {
                int portalIndex = porPtrs[i].Index;
                PortalTraversalData porData = portalTraversalDataArray[portalIndex];
                if (porData.HasMark(PortalTraversalMark.FastMarchTraversed) || !porData.HasMark(PortalTraversalMark.Reduced)) { continue; }
                porData.Mark |= PortalTraversalMark.FastMarchTraversed;
                portalTraversalDataArray[portalIndex] = porData;
                fastMarchingQueue.Enqueue(portalIndex);
            }
            for (int i = por2Ptr; i < por2Ptr + por2Cnt; i++)
            {

                int portalIndex = porPtrs[i].Index;
                PortalTraversalData porData = portalTraversalDataArray[portalIndex];
                if (porData.HasMark(PortalTraversalMark.FastMarchTraversed) || !porData.HasMark(PortalTraversalMark.Reduced)) { continue; }
                porData.Mark |= PortalTraversalMark.FastMarchTraversed;
                portalTraversalDataArray[portalIndex] = porData;
                fastMarchingQueue.Enqueue(portalIndex);
            }
        }
    }
    int RunReductionAStar(int sourcePortalIndex, UnsafeHeap<int> traversalHeap)
    {
        UnsafeList<PortalNode> portalNodes = PortalNodes;
        NativeArray<PortalTraversalData> portalTraversalDataArray = PortalTraversalDataArray;
        PortalTraversalData curData = PortalTraversalDataArray[sourcePortalIndex];
        if (curData.HasMark(PortalTraversalMark.AStarPicked))
        {
            return -1;
        }

        //SET INNITIAL MARK
        curData = new PortalTraversalData()
        {
            FCost = 0,
            GCost = 0,
            HCost = float.MaxValue,
            Mark = curData.Mark | PortalTraversalMark.AStarPicked | PortalTraversalMark.Added | PortalTraversalMark.Reduced,
            OriginIndex = sourcePortalIndex,
            NextIndex = -1,
            DistanceFromTarget = curData.DistanceFromTarget,
        };
        PortalTraversalDataArray[sourcePortalIndex] = curData;

        //NODE DATA
        int curNodeIndex = sourcePortalIndex;
        PortalNode curNode = PortalNodes[curNodeIndex];
        int por1P2pIdx = curNode.Portal1.PorToPorPtr;
        int por2P2pIdx = curNode.Portal2.PorToPorPtr;
        int por1P2pCnt = curNode.Portal1.PorToPorCnt;
        int por2P2pCnt = curNode.Portal2.PorToPorCnt;

        //HANDLE NEIGHBOURS
        AStarTraverseIndexList.Add(curNodeIndex);
        TraverseNeighbours(curData, ref traversalHeap, curNodeIndex, por1P2pIdx, por1P2pIdx + por1P2pCnt);
        TraverseNeighbours(curData, ref traversalHeap, curNodeIndex, por2P2pIdx, por2P2pIdx + por2P2pCnt);
        SetNextNode();

        while (!curData.HasMark(PortalTraversalMark.AStarPicked))
        {
            TraverseNeighbours(curData, ref traversalHeap, curNodeIndex, por1P2pIdx, por1P2pIdx + por1P2pCnt);
            TraverseNeighbours(curData, ref traversalHeap, curNodeIndex, por2P2pIdx, por2P2pIdx + por2P2pCnt);
            SetNextNode();
        }
        return curNodeIndex;
        void SetNextNode()
        {
            if (traversalHeap.IsEmpty) { return; }
            int nextMinIndex = traversalHeap.ExtractMin();
            PortalTraversalData nextMinTraversalData = portalTraversalDataArray[nextMinIndex];
            while (nextMinTraversalData.HasMark(PortalTraversalMark.Extracted))
            {
                nextMinIndex = traversalHeap.ExtractMin();
                nextMinTraversalData = portalTraversalDataArray[nextMinIndex];
            }
            nextMinTraversalData.Mark |= PortalTraversalMark.Extracted;
            curData = nextMinTraversalData;
            portalTraversalDataArray[nextMinIndex] = curData;
            curNodeIndex = nextMinIndex;
            curNode = portalNodes[curNodeIndex];
            por1P2pIdx = curNode.Portal1.PorToPorPtr;
            por2P2pIdx = curNode.Portal2.PorToPorPtr;
            por1P2pCnt = curNode.Portal1.PorToPorCnt;
            por2P2pCnt = curNode.Portal2.PorToPorCnt;
        }
    }
    void TraverseNeighbours(PortalTraversalData curData, ref UnsafeHeap<int> traversalHeap, int curNodeIndex, int from, int to)
    {
        for (int i = from; i < to; i++)
        {
            PortalToPortal neighbourConnection = PorPtrs[i];
            PortalNode portalNode = PortalNodes[neighbourConnection.Index];
            PortalTraversalData traversalData = PortalTraversalDataArray[neighbourConnection.Index];
            if (traversalData.HasMark(PortalTraversalMark.Added))
            {
                float newGCost = curData.GCost + neighbourConnection.Distance;
                if (newGCost < traversalData.GCost)
                {
                    float newFCost = traversalData.HCost + newGCost;
                    traversalData.GCost = newGCost;
                    traversalData.FCost = newFCost;
                    traversalData.OriginIndex = curNodeIndex;
                    PortalTraversalDataArray[neighbourConnection.Index] = traversalData;
                    traversalHeap.Add(neighbourConnection.Index, traversalData.FCost, traversalData.HCost);
                }
            }
            else
            {
                float hCost = GetHCost(portalNode.Portal1.Index);
                float gCost = curData.GCost + neighbourConnection.Distance;
                float fCost = hCost * gCost;
                traversalData.HCost = hCost;
                traversalData.GCost = gCost;
                traversalData.FCost = fCost;
                traversalData.Mark |= PortalTraversalMark.Added | PortalTraversalMark.Reduced;
                traversalData.OriginIndex = curNodeIndex;
                PortalTraversalDataArray[neighbourConnection.Index] = traversalData;
                traversalHeap.Add(neighbourConnection.Index, traversalData.FCost, traversalData.HCost);
                AStarTraverseIndexList.Add(neighbourConnection.Index);
            }
        }
        if (curData.HasMark(PortalTraversalMark.TargetNeighbour))
        {
            int targetNodeIndex = PortalNodes.Length - 1;
            PortalTraversalData traversalData = PortalTraversalDataArray[targetNodeIndex];
            if (traversalData.HasMark(PortalTraversalMark.Added))
            {
                float newGCost = curData.GCost + GetGCostBetweenTargetAndTargetNeighbour(curNodeIndex);
                if (newGCost < traversalData.GCost)
                {
                    float newFCost = traversalData.HCost + newGCost;
                    traversalData.GCost = newGCost;
                    traversalData.FCost = newFCost;
                    traversalData.OriginIndex = curNodeIndex;
                    PortalTraversalDataArray[targetNodeIndex] = traversalData;
                    traversalHeap.Add(targetNodeIndex, traversalData.FCost, traversalData.HCost);
                }
            }
            else
            {
                float hCost = 0f;
                float gCost = curData.GCost + GetGCostBetweenTargetAndTargetNeighbour(curNodeIndex);
                float fCost = hCost + gCost;
                traversalData.HCost = hCost;
                traversalData.GCost = gCost;
                traversalData.FCost = fCost;
                traversalData.Mark |= PortalTraversalMark.Added;
                traversalData.OriginIndex = curNodeIndex;
                PortalTraversalDataArray[targetNodeIndex] = traversalData;
                traversalHeap.Add(targetNodeIndex, traversalData.FCost, traversalData.HCost);
                AStarTraverseIndexList.Add(targetNodeIndex);
            }
        }
    }
    float GetHCost(Index2 nodePos)
    {
        int2 newNodePos = new int2(nodePos.C, nodePos.R);
        int2 targetPos = TargetIndex;
        
        int xDif = math.abs(newNodePos.x - targetPos.x);
        int yDif = math.abs(newNodePos.y - targetPos.y);
        int smallOne = math.min(xDif, yDif);
        int bigOne = math.max(xDif, yDif);
        return (bigOne - smallOne) * 1f + smallOne * 1.4f;
    }
    void SetTargetNeighbourPortalDataAndAddToList()
    {
        SectorNode sectorNode = SectorNodes[_targetSectorIndex1d];
        int winPtr = sectorNode.SecToWinPtr;
        int winCnt = sectorNode.SecToWinCnt;
        for (int i = 0; i < winCnt; i++)
        {
            WindowNode windowNode = WindowNodes[SecToWinPtrs[winPtr + i]];
            int porPtr = windowNode.PorPtr;
            int porCnt = windowNode.PorCnt;
            for (int j = porPtr; j < porCnt + porPtr; j++)
            {
                int portalNodeIndex = j;
                int portalLocalIndexAtSector = GetPortalLocalIndexAtSector(PortalNodes[portalNodeIndex], _targetSectorIndex1d, _targetSectorStartIndex1d);
                float integratedCost = TargetSectorCosts[portalLocalIndexAtSector].IntegratedCost;
                if (integratedCost == float.MaxValue) { continue; }
                PortalTraversalDataArray[portalNodeIndex] = new PortalTraversalData()
                {
                    DistanceFromTarget = integratedCost,
                    FCost = float.MaxValue,
                    GCost = float.MaxValue,
                    HCost = float.MaxValue,
                    OriginIndex = portalNodeIndex,
                    Mark = PortalTraversalMark.TargetNeighbour,
                    NextIndex = -1,
                };
                TargetNeighbourPortalIndicies.Add(portalNodeIndex);
            }
        }
    }
    float GetGCostBetweenTargetAndTargetNeighbour(int targetNeighbourIndex)
    {
        int portalLocalIndexAtSector = GetPortalLocalIndexAtSector(PortalNodes[targetNeighbourIndex], _targetSectorIndex1d, _targetSectorStartIndex1d);
        return TargetSectorCosts[portalLocalIndexAtSector].IntegratedCost;
    }
    NativeArray<DijkstraTile> GetIntegratedCosts(int targetIndex)
    {
        NativeArray<DijkstraTile> integratedCosts = new NativeArray<DijkstraTile>(SectorColAmount * SectorColAmount, Allocator.Temp);
        NativeQueue<int> aStarQueue = new NativeQueue<int>(Allocator.Temp);
        CalculateIntegratedCosts(integratedCosts, aStarQueue, SectorNodes[_targetSectorIndex1d].Sector, targetIndex);
        return integratedCosts;
    }
    void SetSectorPortalIndicies(int targetSectorIndexF, NativeList<int> destinationList)
    {
        SectorNode sectorNode = SectorNodes[targetSectorIndexF];
        int winPtr = sectorNode.SecToWinPtr;
        int winCnt = sectorNode.SecToWinCnt;
        for (int i = 0; i < winCnt; i++)
        {
            WindowNode windowNode = WindowNodes[SecToWinPtrs[winPtr + i]];
            int porPtr = windowNode.PorPtr;
            int porCnt = windowNode.PorCnt;
            for (int j = 0; j < porCnt; j++)
            {
                destinationList.Add(j + porPtr);
            }
        }
    }
    void PickSectorsFromPortalSequence()
    {
        int sectorTileAmount = SectorColAmount * SectorColAmount;
        for (int i = 0; i < PortalSequenceBorders.Length - 1; i++)
        {
            int start = PortalSequenceBorders[i];
            int end = PortalSequenceBorders[i + 1];
            for (int j = start; j < end - 2; j++)
            {
                int portalIndex1 = PortalSequence[j].Index;
                int portalIndex2 = PortalSequence[j + 1].Index;
                int windowIndex1 = PortalNodes[portalIndex1].WinPtr;
                int windowIndex2 = PortalNodes[portalIndex2].WinPtr;
                WindowNode winNode1 = WindowNodes[windowIndex1];
                WindowNode winNode2 = WindowNodes[windowIndex2];
                int win1Sec1Index = WinToSecPtrs[winNode1.WinToSecPtr];
                int win1Sec2Index = WinToSecPtrs[winNode1.WinToSecPtr + 1];
                int win2Sec1Index = WinToSecPtrs[winNode2.WinToSecPtr];
                int win2Sec2Index = WinToSecPtrs[winNode2.WinToSecPtr + 1];
                if ((win1Sec1Index == win2Sec1Index || win1Sec1Index == win2Sec2Index) && SectorToPicked[win1Sec1Index] == 0)
                {
                    SectorToPicked[win1Sec1Index] = PickedToSector.Length * sectorTileAmount + 1;
                    PickedToSector.Add(win1Sec1Index);
                    SectorStateTable[win1Sec1Index] = PathSectorState.Included;
                }
                if ((win1Sec2Index == win2Sec1Index || win1Sec2Index == win2Sec2Index) && SectorToPicked[win1Sec2Index] == 0)
                {
                    SectorToPicked[win1Sec2Index] = PickedToSector.Length * sectorTileAmount + 1;
                    PickedToSector.Add(win1Sec2Index);
                    SectorStateTable[win1Sec2Index] = PathSectorState.Included;
                }
            }

        }
        
    }
    public int GetIsland(int2 general2d)
    {
        int2 sector2d = FlowFieldUtilities.GetSectorIndex(general2d, SectorColAmount);
        int sector1d = FlowFieldUtilities.To1D(sector2d, SectorMatrixColAmount);
        SectorNode sector = SectorNodes[sector1d];

        if (sector.IsIslandValid())
        {
            return PortalNodes[sector.SectorIslandPortalIndex].IslandIndex;
        }
        else if (sector.IsIslandField)
        {
            int2 sectorStart = FlowFieldUtilities.GetSectorStartIndex(sector2d, SectorColAmount);
            int2 local2d = FlowFieldUtilities.GetLocalIndex(general2d, sectorStart);
            int local1d = FlowFieldUtilities.To1D(local2d, SectorColAmount);
            int island = IslandFields[sector1d][local1d];
            switch (island)
            {
                case < 0:
                    return -island;
                case int.MaxValue:
                    return int.MaxValue;
                default:
                    return PortalNodes[island].IslandIndex;
            }
        }
        return int.MaxValue;
    }
    void AddTargetSector()
    {
        int sectorTileAmount = SectorColAmount * SectorColAmount;
        if (SectorToPicked[_targetSectorIndex1d] == 0)
        {
            SectorToPicked[_targetSectorIndex1d] = PickedToSector.Length * sectorTileAmount + 1;
            PickedToSector.Add(_targetSectorIndex1d);
            SectorStateTable[_targetSectorIndex1d] = PathSectorState.Included;
        }
        FlowFieldLength[0] = PickedToSector.Length * sectorTileAmount + 1;
    }
    //HELPERS
    int GetPortalLocalIndexAtSector(PortalNode portalNode, int sectorIndex, int sectorStartIndex)
    {
        Index2 index1 = portalNode.Portal1.Index;
        int index1Flat = index1.R * FieldColAmount + index1.C;
        int index1SectorIndex = (index1.R / SectorColAmount) * SectorMatrixColAmount + (index1.C / SectorColAmount);
        if (sectorIndex == index1SectorIndex)
        {
            return GetLocalIndex(index1Flat, sectorStartIndex);
        }
        Index2 index2 = portalNode.Portal2.Index;
        int index2Flat = index2.R * FieldColAmount + index2.C;
        return GetLocalIndex(index2Flat, sectorStartIndex);
    }
    int GetLocalIndex(int index, int sectorStartIndexF)
    {
        int distanceFromSectorStart = index - sectorStartIndexF;
        return (distanceFromSectorStart % FieldColAmount) + (SectorColAmount * (distanceFromSectorStart / FieldColAmount));
    }

    //TARGET SECTOR COST CALCULATION WITH DIJKSTRA
    void CalculateIntegratedCosts(NativeArray<DijkstraTile> integratedCosts, NativeQueue<int> aStarQueue, Sector sector, int targetIndexFlat)
    {
        //DATA
        int sectorTileAmount = SectorColAmount;
        int fieldColAmount = FieldColAmount;
        int sectorStartIndexFlat = sector.StartIndex.R * fieldColAmount + sector.StartIndex.C;
        UnsafeList<byte> costs = Costs;

        //CODE

        Reset();
        int targetLocalIndex = GetLocalIndex(targetIndexFlat);
        DijkstraTile targetTile = integratedCosts[targetLocalIndex];
        targetTile.IntegratedCost = 0f;
        targetTile.IsAvailable = false;
        integratedCosts[targetLocalIndex] = targetTile;
        Enqueue(LocalDirections[targetLocalIndex]);
        while (!aStarQueue.IsEmpty())
        {
            int localindex = aStarQueue.Dequeue();
            DijkstraTile tile = integratedCosts[localindex];
            tile.IntegratedCost = GetCost(LocalDirections[localindex]);
            integratedCosts[localindex] = tile;
            Enqueue(LocalDirections[localindex]);
        }

        //HELPERS

        void Reset()
        {
            for (int i = 0; i < integratedCosts.Length; i++)
            {
                int generalIndex = GetGeneralIndex(i);
                byte cost = costs[generalIndex];
                if (cost == byte.MaxValue)
                {
                    integratedCosts[i] = new DijkstraTile(cost, float.MaxValue, false);
                    continue;
                }
                integratedCosts[i] = new DijkstraTile(cost, float.MaxValue, true);
            }
        }
        void Enqueue(SectorDirectionData localDirections)
        {
            int n = localDirections.N;
            int e = localDirections.E;
            int s = localDirections.S;
            int w = localDirections.W;
            if (integratedCosts[n].IsAvailable)
            {
                aStarQueue.Enqueue(n);
                DijkstraTile tile = integratedCosts[n];
                tile.IsAvailable = false;
                integratedCosts[n] = tile;
            }
            if (integratedCosts[e].IsAvailable)
            {
                aStarQueue.Enqueue(e);
                DijkstraTile tile = integratedCosts[e];
                tile.IsAvailable = false;
                integratedCosts[e] = tile;
            }
            if (integratedCosts[s].IsAvailable)
            {
                aStarQueue.Enqueue(s);
                DijkstraTile tile = integratedCosts[s];
                tile.IsAvailable = false;
                integratedCosts[s] = tile;
            }
            if (integratedCosts[w].IsAvailable)
            {
                aStarQueue.Enqueue(w);
                DijkstraTile tile = integratedCosts[w];
                tile.IsAvailable = false;
                integratedCosts[w] = tile;
            }
        }
        float GetCost(SectorDirectionData localDirections)
        {
            float costToReturn = float.MaxValue;
            float nCost = integratedCosts[localDirections.N].IntegratedCost + 1f;
            float neCost = integratedCosts[localDirections.NE].IntegratedCost + 1.4f;
            float eCost = integratedCosts[localDirections.E].IntegratedCost + 1f;
            float seCost = integratedCosts[localDirections.SE].IntegratedCost + 1.4f;
            float sCost = integratedCosts[localDirections.S].IntegratedCost + 1f;
            float swCost = integratedCosts[localDirections.SW].IntegratedCost + 1.4f;
            float wCost = integratedCosts[localDirections.W].IntegratedCost + 1f;
            float nwCost = integratedCosts[localDirections.NW].IntegratedCost + 1.4f;
            if (nCost < costToReturn) { costToReturn = nCost; }
            if (neCost < costToReturn) { costToReturn = neCost; }
            if (eCost < costToReturn) { costToReturn = eCost; }
            if (seCost < costToReturn) { costToReturn = seCost; }
            if (sCost < costToReturn) { costToReturn = sCost; }
            if (swCost < costToReturn) { costToReturn = swCost; }
            if (wCost < costToReturn) { costToReturn = wCost; }
            if (nwCost < costToReturn) { costToReturn = nwCost; }
            return costToReturn;
        }
        int GetGeneralIndex(int index)
        {
            return sectorStartIndexFlat + (index / sectorTileAmount * fieldColAmount) + (index % sectorTileAmount);
        }
        int GetLocalIndex(int index)
        {
            int distanceFromSectorStart = index - sectorStartIndexFlat;
            return (distanceFromSectorStart % fieldColAmount) + (sectorTileAmount * (distanceFromSectorStart / fieldColAmount));
        }
    }
    public struct UnsafeHeap<T> where T : unmanaged
    {
        public UnsafeList<HeapElement<T>> _array;
        public T this[int index]
        {
            get
            {
                return _array[index].data;
            }
        }
        public bool IsEmpty
        {
            get
            {
                return _array.IsEmpty;
            }
        }
        public UnsafeHeap(int size, Allocator allocator)
        {
            _array = new UnsafeList<HeapElement<T>>(size, allocator);
        }
        public void Clear()
        {
            _array.Clear();
        }
        public void Add(T element, float pri1, float pri2)
        {
            int elementIndex = _array.Length;
            _array.Add(new HeapElement<T>(element, pri1, pri2));
            if (elementIndex != 0)
            {
                HeapifyUp(elementIndex);
            }
        }
        public T GetMin() => _array[0].data;
        public T ExtractMin()
        {
            T min = _array[0].data;
            HeapElement<T> last = _array[_array.Length - 1];
            _array[0] = last;
            _array.Length--;
            if (_array.Length > 1)
            {
                HeapifyDown(0);
            }
            return min;
        }
        public void SetPriority(int index, float pri1)
        {
            int length = _array.Length;
            HeapElement<T> cur = _array[index];
            cur.pri1 = pri1;
            _array[index] = cur;
            int parIndex = index / 2 - 1;
            int lcIndex = index * 2 + 1;
            int rcIndex = index * 2 + 2;
            parIndex = math.select(index, parIndex, parIndex >= 0);
            lcIndex = math.select(index, lcIndex, lcIndex < length);
            rcIndex = math.select(index, rcIndex, rcIndex < length);
            HeapElement<T> parent = _array[parIndex];
            if (cur.pri1 < parent.pri1 || (cur.pri1 == parent.pri1 && cur.pri2 < parent.pri2))
            {
                HeapifyUp(index);
            }
            else
            {
                HeapifyDown(index);
            }
        }
        public void Dispose()
        {
            _array.Dispose();
        }

        void HeapifyUp(int startIndex)
        {
            int curIndex = startIndex;
            int parIndex = (curIndex - 1) / 2;
            HeapElement<T> cur = _array[startIndex];
            HeapElement<T> par = _array[parIndex];
            bool isCurSmaller = cur.pri1 < par.pri1 || (cur.pri1 == par.pri1 && cur.pri2 < par.pri2);
            while (isCurSmaller)
            {
                _array[parIndex] = cur;
                _array[curIndex] = par;
                curIndex = parIndex;
                parIndex = math.select((curIndex - 1) / 2, 0, curIndex == 0);
                par = _array[parIndex];
                isCurSmaller = cur.pri1 < par.pri1 || (cur.pri1 == par.pri1 && cur.pri2 < par.pri2);
            }
        }
        void HeapifyDown(int startIndex)
        {
            int length = _array.Length;
            int curIndex = startIndex;
            int lcIndex = startIndex * 2 + 1;
            int rcIndex = lcIndex + 1;
            lcIndex = math.select(curIndex, lcIndex, lcIndex < length);
            rcIndex = math.select(curIndex, rcIndex, rcIndex < length);
            HeapElement<T> cur;
            HeapElement<T> lc;
            HeapElement<T> rc;
            while (lcIndex != curIndex)
            {
                cur = _array[curIndex];
                lc = _array[lcIndex];
                rc = _array[rcIndex];
                bool lcSmallerThanRc = lc.pri1 < rc.pri1 || (lc.pri1 == rc.pri1 && lc.pri2 < rc.pri2);
                bool lcSmallerThanCur = lc.pri1 < cur.pri1 || (lc.pri1 == cur.pri1 && lc.pri2 < cur.pri2);
                bool rcSmallerThanCur = rc.pri1 < cur.pri1 || (rc.pri1 == cur.pri1 && rc.pri2 < cur.pri2);

                if (lcSmallerThanRc && lcSmallerThanCur)
                {
                    _array[curIndex] = lc;
                    _array[lcIndex] = cur;
                    curIndex = lcIndex;
                    lcIndex = curIndex * 2 + 1;
                    rcIndex = lcIndex + 1;
                    lcIndex = math.select(lcIndex, curIndex, lcIndex >= length);
                    rcIndex = math.select(rcIndex, curIndex, rcIndex >= length);
                }
                else if (!lcSmallerThanRc && rcSmallerThanCur)
                {
                    _array[curIndex] = rc;
                    _array[rcIndex] = cur;
                    curIndex = rcIndex;
                    lcIndex = curIndex * 2 + 1;
                    rcIndex = lcIndex + 1;
                    lcIndex = math.select(lcIndex, curIndex, lcIndex >= length);
                    rcIndex = math.select(rcIndex, curIndex, rcIndex >= length);
                }
                else
                {
                    break;
                }
            }
        }
        public struct HeapElement<T> where T : unmanaged
        {
            public T data;
            public float pri1;
            public float pri2;

            public HeapElement(T data, float pri1, float pri2)
            {
                this.data = data;
                this.pri1 = pri1;
                this.pri2 = pri2;
            }
        }
    }
}
[BurstCompile]
public struct ActivePortal
{
    public int Index;
    public int NextIndex;
    public float Distance;

    public bool IsTargetNode() => Index == -1 && Distance == 0 && NextIndex == -1;
    public bool IsTargetNeighbour() => NextIndex == -1;
    public void SetTerminator()
    {
        Index = -1;
        Distance = -1;
        NextIndex = -1;
    }
    public bool IsTermintor()
    {
        return Index == -1 && Distance == -1 && NextIndex == -1;
    }
    public static ActivePortal GetTargetNode()
    {
        return new ActivePortal()
        {
            Index = -1,
            Distance = 0,
            NextIndex = -1
        };
    }
}