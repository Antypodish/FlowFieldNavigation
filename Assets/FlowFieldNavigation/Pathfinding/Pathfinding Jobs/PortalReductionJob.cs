using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;

namespace FlowFieldNavigation
{
    [BurstCompile]
    internal struct PortalReductionJob : IJob
    {
        internal int2 TargetIndex;
        internal int FieldColAmount;
        internal int FieldRowAmount;
        internal float FieldTileSize;
        internal int SectorColAmount;
        internal int SectorMatrixColAmount;
        internal int SectorTileAmount;
        internal float2 FieldGridStartPos;

        internal NativeArray<PortalTraversalData> PortalTraversalDataArray;

        internal UnsafeList<PathSectorState> SectorStateTable;
        internal UnsafeList<int> SectorToPicked;
        internal NativeList<int> PickedToSector;
        internal UnsafeList<DijkstraTile> TargetSectorCosts;
        internal NativeReference<int> FlowFieldLength;

        [ReadOnly] internal NativeSlice<float2> SourcePositions;
        [ReadOnly] internal NativeArray<SectorNode> SectorNodes;
        [ReadOnly] internal NativeArray<int> SecToWinPtrs;
        [ReadOnly] internal NativeArray<WindowNode> WindowNodes;
        [ReadOnly] internal NativeArray<int> WinToSecPtrs;
        [ReadOnly] internal NativeArray<PortalNode> PortalNodes;
        [ReadOnly] internal NativeArray<PortalToPortal> PorPtrs;
        [ReadOnly] internal NativeArray<byte> Costs;
        [ReadOnly] internal NativeArray<SectorDirectionData> LocalDirections;
        [ReadOnly] internal NativeArray<UnsafeList<int>> IslandFields;

        internal NativeList<int> TargetNeighbourPortalIndicies;
        internal NativeList<int> AStarTraverseIndexList;
        internal NativeList<int> SourcePortalIndexList;
        internal NativeList<int> DijkstraStartIndicies;

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
            if(PickedToSector.Length == 0)
            {
                SetIntegratedCosts(targetGeneralIndex1d);
                PortalTraversalData destinationData = PortalTraversalDataArray[PortalTraversalDataArray.Length - 1];
                destinationData.DistanceFromTarget = 0;
                PortalTraversalDataArray[PortalTraversalDataArray.Length - 1] = destinationData;
                SetTargetNeighbourPortalDataAndAddToList();
            }

            if (TargetNeighbourPortalIndicies.Length == 0)
            {
                return;
            }

            DoubleFloatUnsafeHeap<int> walkerHeap = new DoubleFloatUnsafeHeap<int>(10, Allocator.Temp);
            SourcePortalIndexList.Clear();
            SetSourcePortalIndicies();
            NativeArray<int> sourcePortalsAsArray = SourcePortalIndexList.AsArray();
            for (int i = 0; i < sourcePortalsAsArray.Length; i++)
            {
                int stoppedIndex = RunReductionAStar(sourcePortalsAsArray[i], walkerHeap);
                if (stoppedIndex == -1) { continue; }
                PickAStarNodes(SourcePortalIndexList[i], stoppedIndex);
                ResetTraversedIndicies();
                walkerHeap.Clear();
            }
        }
        void ResetTraversedIndicies()
        {
            PortalTraversalMark bitsToSet = ~(PortalTraversalMark.AStarTraversed | PortalTraversalMark.AStarExtracted);
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
        int RunReductionAStar(int sourcePortalIndex, DoubleFloatUnsafeHeap<int> traversalHeap)
        {
            NativeArray<PortalNode> portalNodes = PortalNodes;
            NativeArray<PortalTraversalData> portalTraversalDataArray = PortalTraversalDataArray;
            PortalTraversalData curData = PortalTraversalDataArray[sourcePortalIndex];
            if (curData.HasMark(PortalTraversalMark.AStarPicked) || curData.HasMark(PortalTraversalMark.DijkstraTraversed))
            {
                return -1;
            }

            //Set initial portal
            curData.Mark |= PortalTraversalMark.AStarPicked | PortalTraversalMark.AStarTraversed | PortalTraversalMark.AStarExtracted | PortalTraversalMark.Reduced;
            curData.OriginIndex = sourcePortalIndex;
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
            NativeSlice<PortalToPortal> neighbourIndicies1 = new NativeSlice<PortalToPortal>(PorPtrs, por1P2pIdx, por1P2pCnt);
            NativeSlice<PortalToPortal> neighbourIndicies2 = new NativeSlice<PortalToPortal>(PorPtrs, por2P2pIdx, por2P2pCnt);
            TraverseNeighbours(curData, ref traversalHeap, curNodeIndex, neighbourIndicies1);
            TraverseNeighbours(curData, ref traversalHeap, curNodeIndex, neighbourIndicies2);
            SetNextNode();

            while (!(IsGoal(curData.DistanceFromTarget) || ShouldMerge(curData.Mark)))
            {
                neighbourIndicies1 = new NativeSlice<PortalToPortal>(PorPtrs, por1P2pIdx, por1P2pCnt);
                neighbourIndicies2 = new NativeSlice<PortalToPortal>(PorPtrs, por2P2pIdx, por2P2pCnt);
                TraverseNeighbours(curData, ref traversalHeap, curNodeIndex, neighbourIndicies1);
                TraverseNeighbours(curData, ref traversalHeap, curNodeIndex, neighbourIndicies2);
                SetNextNode();
            }
            return curNodeIndex;
            void SetNextNode()
            {
                if (traversalHeap.IsEmpty) { return; }
                int nextMinIndex = traversalHeap.ExtractMin();
                PortalTraversalData nextMinTraversalData = portalTraversalDataArray[nextMinIndex];
                while (nextMinTraversalData.HasMark(PortalTraversalMark.AStarExtracted))
                {
                    nextMinIndex = traversalHeap.ExtractMin();
                    nextMinTraversalData = portalTraversalDataArray[nextMinIndex];
                }
                nextMinTraversalData.Mark |= PortalTraversalMark.AStarExtracted;
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
        void TraverseNeighbours(PortalTraversalData curData, ref DoubleFloatUnsafeHeap<int> traversalHeap, int curNodeIndex, NativeSlice<PortalToPortal> neihgbourIndicies)
        {
            bool curDijkstraTraversed = curData.HasMark(PortalTraversalMark.DijkstraTraversed);
            for (int i = 0; i < neihgbourIndicies.Length; i++)
            {
                PortalToPortal neighbourConnection = neihgbourIndicies[i];
                PortalNode portalNode = PortalNodes[neighbourConnection.Index];
                PortalTraversalData traversalData = PortalTraversalDataArray[neighbourConnection.Index];
                if (traversalData.HasMark(PortalTraversalMark.AStarTraversed))
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
                    float fCost = hCost + gCost;
                    traversalData.HCost = hCost;
                    traversalData.GCost = gCost;
                    traversalData.FCost = fCost;
                    traversalData.Mark |= PortalTraversalMark.AStarTraversed | PortalTraversalMark.Reduced;
                    traversalData.OriginIndex = curNodeIndex;
                    PortalTraversalDataArray[neighbourConnection.Index] = traversalData;
                    traversalHeap.Add(neighbourConnection.Index, traversalData.FCost, traversalData.HCost);
                    AStarTraverseIndexList.Add(neighbourConnection.Index);

                    bool neighbourDijkstraTraversed = traversalData.HasMark(PortalTraversalMark.DijkstraTraversed);
                    if (!curDijkstraTraversed && neighbourDijkstraTraversed)
                    {
                        DijkstraStartIndicies.Add(neighbourConnection.Index);
                    }
                }
            }
            if (curData.HasMark(PortalTraversalMark.TargetNeighbour))
            {
                int targetNodeIndex = PortalNodes.Length - 1;
                PortalTraversalData traversalData = PortalTraversalDataArray[targetNodeIndex];
                if (traversalData.HasMark(PortalTraversalMark.AStarTraversed))
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
                    traversalData.Mark |= PortalTraversalMark.AStarTraversed;
                    traversalData.OriginIndex = curNodeIndex;
                    PortalTraversalDataArray[targetNodeIndex] = traversalData;
                    traversalHeap.Add(targetNodeIndex, traversalData.FCost, traversalData.HCost);
                    AStarTraverseIndexList.Add(targetNodeIndex);
                }
            }
        }
        bool IsGoal(float distanceToDestination)
        {
            return distanceToDestination == 0;
        }
        bool ShouldMerge(PortalTraversalMark traversalMarks)
        {
            return (traversalMarks & PortalTraversalMark.AStarPicked) == PortalTraversalMark.AStarPicked;
        }
        float GetGCostBetweenTargetAndTargetNeighbour(int targetNeighbourIndex)
        {
            int portalLocalIndexAtSector = FlowFieldUtilities.GetLocal1dInSector(PortalNodes[targetNeighbourIndex], _targetSectorIndex1d, SectorMatrixColAmount, SectorColAmount);
            return TargetSectorCosts[portalLocalIndexAtSector].IntegratedCost;
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
        internal int GetIsland(int2 general2d)
        {
            int2 sector2d = FlowFieldUtilities.GetSector2D(general2d, SectorColAmount);
            int sector1d = FlowFieldUtilities.To1D(sector2d, SectorMatrixColAmount);
            SectorNode sector = SectorNodes[sector1d];

            if (sector.IsIslandValid())
            {
                return PortalNodes[sector.SectorIslandPortalIndex].IslandIndex;
            }
            else if (sector.IsIslandField)
            {
                int2 sectorStart = FlowFieldUtilities.GetSectorStartIndex(sector2d, SectorColAmount);
                int2 local2d = FlowFieldUtilities.GetLocal2D(general2d, sectorStart);
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
        void SetSourcePortalIndicies()
        {
            int targetIsland = GetIsland(TargetIndex);
            int sectorTileAmount = SectorColAmount * SectorColAmount;
            for (int i = 0; i < SourcePositions.Length; i++)
            {
                float2 sourcePos = SourcePositions[i];
                int2 sourceIndex = FlowFieldUtilities.PosTo2D(sourcePos, FieldTileSize, FieldGridStartPos);
                int2 sourceSectorIndex = sourceIndex / SectorColAmount;
                int sourceSectorIndexFlat = sourceSectorIndex.y * SectorMatrixColAmount + sourceSectorIndex.x;
                //ADD SOURCE SECTOR TO THE PICKED SECTORS
                PathSectorState sectorState = SectorStateTable[sourceSectorIndexFlat];
                if ((sectorState & PathSectorState.Included) != PathSectorState.Included)
                {
                    SectorToPicked[sourceSectorIndexFlat] = PickedToSector.Length * sectorTileAmount + 1;
                    PickedToSector.Add(sourceSectorIndexFlat);
                    SectorStateTable[sourceSectorIndexFlat] |= PathSectorState.Included | PathSectorState.Source;
                    SetSectorPortalIndicies(sourceSectorIndexFlat, SourcePortalIndexList, targetIsland);
                }
                else if ((sectorState & PathSectorState.Source) != PathSectorState.Source)
                {
                    SectorStateTable[sourceSectorIndexFlat] |= PathSectorState.Source;
                    SetSectorPortalIndicies(sourceSectorIndexFlat, SourcePortalIndexList, targetIsland);
                }
            }
        }
        void SetSectorPortalIndicies(int targetSectorIndexF, NativeList<int> destinationList, int targetIsland)
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
                    if (PortalNodes[j + porPtr].IslandIndex != targetIsland) { continue; }
                    destinationList.Add(j + porPtr);
                }
            }
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
                    int portalLocalIndexAtSector = FlowFieldUtilities.GetLocal1dInSector(PortalNodes[portalNodeIndex], _targetSectorIndex1d, SectorMatrixColAmount, SectorColAmount);
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
        void SetIntegratedCosts(int targetIndex)
        {
            NativeQueue<int> aStarQueue = new NativeQueue<int>(Allocator.Temp);
            CalculateIntegratedCosts(TargetSectorCosts, aStarQueue, SectorNodes[_targetSectorIndex1d].Sector, targetIndex);
        }
        void CalculateIntegratedCosts(UnsafeList<DijkstraTile> integratedCosts, NativeQueue<int> aStarQueue, Sector sector, int targetIndexFlat)
        {
            //DATA
            int2 targetIndex2d = FlowFieldUtilities.To2D(targetIndexFlat, FieldColAmount);
            NativeSlice<byte> costs = new NativeSlice<byte>(Costs, _targetSectorIndex1d * SectorTileAmount, SectorTileAmount);

            //CODE

            Reset();
            int targetLocalIndex = FlowFieldUtilities.GetLocal1D(targetIndex2d, SectorColAmount, SectorMatrixColAmount).index;
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
                    byte cost = costs[i];
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
        }
    }
}
