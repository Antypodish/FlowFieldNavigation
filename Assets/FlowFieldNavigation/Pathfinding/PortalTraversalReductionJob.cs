using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;
using System;
using System.IO;

[BurstCompile]
internal struct PortalTraversalReductionJob : IJob
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

    internal UnsafeList<int> SectorToPicked;
    internal UnsafeList<PathSectorState> SectorStateTable;
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

        SetIntegratedCosts(targetGeneralIndex1d);

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

        if (TargetNeighbourPortalIndicies.Length == 0)
        {
            AddTargetSector();
            return;
        }

        //START GRAPH WALKER
        DoubleUnsafeHeap<int> walkerHeap = new DoubleUnsafeHeap<int>(10, Allocator.Temp);

        SetSourcePortalIndicies();
        for (int i = 0; i < SourcePortalIndexList.Length; i++)
        {
            int stoppedIndex = RunReductionAStar(SourcePortalIndexList[i], walkerHeap);
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
    void SetSourcePortalIndicies()
    {
        int targetIsland = GetIsland(TargetIndex);
        int sectorTileAmount = SectorColAmount * SectorColAmount;
        for (int i = 0; i < SourcePositions.Length; i++)
        {
            float2 sourcePos = SourcePositions[i];
            int sourceSectorIndexFlat = FlowFieldUtilities.PosToSector1D(sourcePos, SectorColAmount * FieldTileSize, SectorMatrixColAmount, FieldGridStartPos);

            //ADD SOURCE SECTOR TO THE PICKED SECTORS
            if (SectorToPicked[sourceSectorIndexFlat] != 0) { continue; }
            SectorToPicked[sourceSectorIndexFlat] = PickedToSector.Length * sectorTileAmount + 1;
            PickedToSector.Add(sourceSectorIndexFlat);
            SectorStateTable[sourceSectorIndexFlat] |= PathSectorState.Included | PathSectorState.Source;
            //ADD SOURCE SECTOR TO THE PICKED SECTORS
            SetSectorPortalIndicies(sourceSectorIndexFlat, SourcePortalIndexList, targetIsland);
        }
    }
    int RunReductionAStar(int sourcePortalIndex, DoubleUnsafeHeap<int> traversalHeap)
    {
        NativeArray<PortalNode> portalNodes = PortalNodes;
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
            Mark = curData.Mark | PortalTraversalMark.AStarPicked | PortalTraversalMark.AStarTraversed | PortalTraversalMark.Reduced,
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
    void TraverseNeighbours(PortalTraversalData curData, ref DoubleUnsafeHeap<int> traversalHeap, int curNodeIndex, int from, int to)
    {
        for (int i = from; i < to; i++)
        {
            PortalToPortal neighbourConnection = PorPtrs[i];
            PortalNode portalNode = PortalNodes[neighbourConnection.Index];
            PortalTraversalData traversalData = PortalTraversalDataArray[neighbourConnection.Index];
            if (traversalData.HasMark(PortalTraversalMark.AStarTraversed))
            {
                float newGCost = curData.GCost + neighbourConnection.Distance;
                if (newGCost < traversalData.GCost)
                {
                    float newFCost = traversalData.HCost + newGCost;
                    PortalTraversalDataArray[neighbourConnection.Index] = new PortalTraversalData()
                    {
                        HCost = traversalData.HCost,
                        GCost = newGCost,
                        FCost = newFCost,
                        OriginIndex = curNodeIndex,
                        Mark = traversalData.Mark,
                        DistanceFromTarget = traversalData.DistanceFromTarget,
                        NextIndex = traversalData.NextIndex
                    };
                    traversalHeap.Add(neighbourConnection.Index, newFCost, traversalData.HCost);
                }
            }
            else
            {
                float hCost = GetHCost(portalNode.Portal1.Index);
                float gCost = curData.GCost + neighbourConnection.Distance;
                PortalTraversalDataArray[neighbourConnection.Index] = new PortalTraversalData()
                {
                    HCost = hCost,
                    GCost = gCost,
                    FCost = hCost + gCost,
                    Mark = traversalData.Mark | PortalTraversalMark.AStarTraversed | PortalTraversalMark.Reduced,
                    DistanceFromTarget = traversalData.DistanceFromTarget,
                    NextIndex = traversalData.NextIndex,
                    OriginIndex = curNodeIndex
                };
                traversalHeap.Add(neighbourConnection.Index, hCost + gCost, hCost);
                AStarTraverseIndexList.Add(neighbourConnection.Index);
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
    void SetIntegratedCosts(int targetIndex)
    {
        NativeQueue<int> aStarQueue = new NativeQueue<int>(Allocator.Temp);
        CalculateIntegratedCosts(TargetSectorCosts, aStarQueue, SectorNodes[_targetSectorIndex1d].Sector, targetIndex);
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
    void AddTargetSector()
    {
        int sectorTileAmount = SectorColAmount * SectorColAmount;
        if (SectorToPicked[_targetSectorIndex1d] == 0)
        {
            SectorToPicked[_targetSectorIndex1d] = PickedToSector.Length * sectorTileAmount + 1;
            PickedToSector.Add(_targetSectorIndex1d);
            SectorStateTable[_targetSectorIndex1d] |= PathSectorState.Included;
        }
        FlowFieldLength.Value = PickedToSector.Length * sectorTileAmount + 1;
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
    private struct DoubleUnsafeHeap<T> where T : unmanaged
    {
        internal UnsafeList<HeapElement<T>> _array;
        internal T this[int index]
        {
            get
            {
                return _array[index].data;
            }
        }
        internal bool IsEmpty
        {
            get
            {
                return _array.IsEmpty;
            }
        }
        internal DoubleUnsafeHeap(int size, Allocator allocator)
        {
            _array = new UnsafeList<HeapElement<T>>(size, allocator);
        }
        internal void Clear()
        {
            _array.Clear();
        }
        internal void Add(T element, float pri1, float pri2)
        {
            int elementIndex = _array.Length;
            _array.Add(new HeapElement<T>(element, pri1, pri2));
            if (elementIndex != 0)
            {
                HeapifyUp(elementIndex);
            }
        }
        internal T GetMin() => _array[0].data;
        internal T ExtractMin()
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
        internal void SetPriority(int index, float pri1)
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
        internal void Dispose()
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
        internal struct HeapElement<T> where T : unmanaged
        {
            internal T data;
            internal float pri1;
            internal float pri2;

            internal HeapElement(T data, float pri1, float pri2)
            {
                this.data = data;
                this.pri1 = pri1;
                this.pri2 = pri2;
            }
        }
    }
}