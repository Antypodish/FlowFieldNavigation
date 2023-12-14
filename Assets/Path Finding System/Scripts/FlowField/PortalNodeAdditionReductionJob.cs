using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;
using System;
using static UnityEngine.GraphicsBuffer;
using System.IO;

[BurstCompile]
public struct PortalNodeAdditionReductionJob : IJob 
{
    public int2 TargetIndex;
    public float FieldTileSize;
    public int SectorColAmount;
    public int SectorMatrixColAmount;

    public NativeArray<PortalTraversalData> PortalTraversalDataArray;
    public UnsafeList<PathSectorState> SectorStateTable;
    public NativeList<int> PickedToSector;
    public UnsafeList<DijkstraTile> TargetSectorCosts;

    [ReadOnly] public NativeSlice<float2> SourcePositions;
    [ReadOnly] public UnsafeList<SectorNode> SectorNodes;
    [ReadOnly] public NativeArray<int> SecToWinPtrs;
    [ReadOnly] public NativeArray<WindowNode> WindowNodes;
    [ReadOnly] public NativeArray<int> WinToSecPtrs;
    [ReadOnly] public UnsafeList<PortalNode> PortalNodes;
    [ReadOnly] public NativeArray<PortalToPortal> PorPtrs;
    [ReadOnly] public UnsafeList<UnsafeList<int>> IslandFields;

    public NativeList<int> AStarTraverseIndexList;
    public NativeList<int> SourcePortalIndexList;
    public NativeList<int> DijkstraStartIndicies;
    int _targetSectorIndex1d;
    public void Execute()
    {
        //TARGET DATA
        int2 targetSectorIndex2d = new int2(TargetIndex.x / SectorColAmount, TargetIndex.y / SectorColAmount);
        _targetSectorIndex1d = targetSectorIndex2d.y * SectorMatrixColAmount + targetSectorIndex2d.x;

        //START GRAPH WALKER
        DoubleUnsafeHeap<int> walkerHeap = new DoubleUnsafeHeap<int>(10, Allocator.Temp);
        SourcePortalIndexList.Clear();
        SetSourcePortalIndicies();
        NativeArray<int> sourcePortalsAsArray = SourcePortalIndexList;
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
    void SetSourcePortalIndicies()
    {
        int targetIsland = GetIsland(TargetIndex);
        int sectorTileAmount = SectorColAmount * SectorColAmount;
        for (int i = 0; i < SourcePositions.Length; i++)
        {
            float2 sourcePos = SourcePositions[i];
            int2 sourceIndex = new int2((int)math.floor(sourcePos.x / FieldTileSize), (int)math.floor(sourcePos.y / FieldTileSize));
            int2 sourceSectorIndex = sourceIndex / SectorColAmount;
            int sourceSectorIndexFlat = sourceSectorIndex.y * SectorMatrixColAmount + sourceSectorIndex.x;

            //ADD SOURCE SECTOR TO THE PICKED SECTORS
            PathSectorState sectorState = SectorStateTable[sourceSectorIndexFlat];
            if((sectorState & PathSectorState.Included) != PathSectorState.Included)
            {
                PickedToSector.Add(sourceSectorIndexFlat);
                SectorStateTable[sourceSectorIndexFlat] |= PathSectorState.Included | PathSectorState.Source;
                SetSectorPortalIndicies(sourceSectorIndexFlat, SourcePortalIndexList, targetIsland);
            }
            else if((sectorState & PathSectorState.Source) != PathSectorState.Source)
            {
                SectorStateTable[sourceSectorIndexFlat] |= PathSectorState.Source;
                SetSectorPortalIndicies(sourceSectorIndexFlat, SourcePortalIndexList, targetIsland);
            }            
        }
    }
    int RunReductionAStar(int sourcePortalIndex, DoubleUnsafeHeap<int> traversalHeap)
    {
        UnsafeList<PortalNode> portalNodes = PortalNodes;
        NativeArray<PortalTraversalData> portalTraversalDataArray = PortalTraversalDataArray;
        PortalTraversalData curData = PortalTraversalDataArray[sourcePortalIndex];
        if (curData.HasMark(PortalTraversalMark.AStarPicked) || curData.HasMark(PortalTraversalMark.DijkstraTraversed))
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
        bool isCurDijkstraTraversed = curData.HasMark(PortalTraversalMark.DijkstraTraversed);
        TraverseNeighbours(curData, ref traversalHeap, curNodeIndex, por1P2pIdx, por1P2pIdx + por1P2pCnt, isCurDijkstraTraversed);
        TraverseNeighbours(curData, ref traversalHeap, curNodeIndex, por2P2pIdx, por2P2pIdx + por2P2pCnt, isCurDijkstraTraversed);
        SetNextNode();

        while (!curData.HasMark(PortalTraversalMark.AStarPicked))
        {
            isCurDijkstraTraversed = curData.HasMark(PortalTraversalMark.DijkstraTraversed);
            TraverseNeighbours(curData, ref traversalHeap, curNodeIndex, por1P2pIdx, por1P2pIdx + por1P2pCnt, isCurDijkstraTraversed);
            TraverseNeighbours(curData, ref traversalHeap, curNodeIndex, por2P2pIdx, por2P2pIdx + por2P2pCnt, isCurDijkstraTraversed);
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
    void TraverseNeighbours(PortalTraversalData curData, ref DoubleUnsafeHeap<int> traversalHeap, int curNodeIndex, int from, int to, bool curDijkstraTraversed)
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
    float GetGCostBetweenTargetAndTargetNeighbour(int targetNeighbourIndex)
    {
        int portalLocalIndexAtSector = FlowFieldUtilities.GetLocal1dInSector(PortalNodes[targetNeighbourIndex], _targetSectorIndex1d, SectorMatrixColAmount, SectorColAmount);
        return TargetSectorCosts[portalLocalIndexAtSector].IntegratedCost;
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
    public int GetIsland(int2 general2d)
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
    public struct DoubleUnsafeHeap<T> where T : unmanaged
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
        public DoubleUnsafeHeap(int size, Allocator allocator)
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