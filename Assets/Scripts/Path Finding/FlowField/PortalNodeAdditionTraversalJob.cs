using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using UnityEngine;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct PortalNodeAdditionTraversalJob : IJob
{
    public int2 TargetIndex;
    public int FieldColAmount;
    public int SectorColAmount;
    public int SectorMatrixColAmount;
    public int ExistingFlowFieldLength;

    public NativeList<LocalIndex1d> IntegrationStartIndicies;
    public NativeArray<PortalTraversalData> PortalTraversalDataArray;
    public NativeList<int> PortalSequence;
    public NativeList<int> PortalSequenceBorders;
    public NativeArray<int> SectorToPicked;
    public NativeList<int> PickedToSector;
    public NativeArray<int> NewFlowFieldLength;

    [ReadOnly] public NativeArray<SectorNode> SectorNodes;
    [ReadOnly] public NativeArray<int> SecToWinPtrs;
    [ReadOnly] public NativeArray<WindowNode> WindowNodes;
    [ReadOnly] public NativeArray<int> WinToSecPtrs;
    [ReadOnly] public NativeArray<PortalNode> PortalNodes;
    [ReadOnly] public NativeArray<PortalToPortal> PorPtrs;
    [ReadOnly] public NativeArray<DijkstraTile> TargetSectorCosts;
    [ReadOnly] public NativeList<int> NewSectors;
    
    int _targetSectorStartIndex1d;
    int _targetSectorIndex1d;
    int _newSequenceStartIndex;
    public void Execute()
    {
        _newSequenceStartIndex = PortalSequence.Length;
        int2 targetSectorIndex2d = new int2(TargetIndex.x / SectorColAmount, TargetIndex.y / SectorColAmount);
        _targetSectorIndex1d = targetSectorIndex2d.y * SectorMatrixColAmount + targetSectorIndex2d.x;
        int2 _targetSectorStartIndex2d = targetSectorIndex2d * SectorColAmount;
        _targetSectorStartIndex1d = _targetSectorStartIndex2d.y * FieldColAmount + _targetSectorStartIndex2d.x;

        //START GRAPH WALKER
        UnsafeList<int> traversedIndicies = new UnsafeList<int>(10, Allocator.Temp);
        UnsafeHeap<int> walkerHeap = new UnsafeHeap<int>(10, Allocator.Temp);
        for (int i = 0; i < NewSectors.Length; i++)
        {
            int sourceSectorIndexFlat = NewSectors[i];
            UnsafeList<int> sourcePortalIndicies = GetPortalIndicies(sourceSectorIndexFlat);
            for (int j = 0; j < sourcePortalIndicies.Length; j++)
            {
                int stoppedIndex = RunGraphWalkerFrom(sourcePortalIndicies[j], walkerHeap, TargetSectorCosts, ref traversedIndicies);
                if (stoppedIndex == -1) { continue; }
                SetPortalSequence(sourcePortalIndicies[j], stoppedIndex);
                ResetTraversedIndicies(ref traversedIndicies);
            }
        }
        PickSectorsFromPortalSequence();
    }
    void ResetTraversedIndicies(ref UnsafeList<int> traversedIndicies)
    {
        PortalTraversalMark bitsToSet = ~(PortalTraversalMark.Included | PortalTraversalMark.Considered);
        for (int i = 0; i < traversedIndicies.Length; i++)
        {
            int index = traversedIndicies[i];
            PortalTraversalData travData = PortalTraversalDataArray[index];
            travData.mark &= bitsToSet;
            PortalTraversalDataArray[index] = travData;
        }
        traversedIndicies.Clear();
    }
    void SetPortalSequence(int sourceNodeIndex, int stoppedIndex)
    {
        bool isIntegrationStartFound = false;
        int originIndex = stoppedIndex;
        while (originIndex != sourceNodeIndex)
        {
            if (!isIntegrationStartFound)
            {
                LocalIndex1d integrationStart = GetNotCalculatedIndexOfPortalNode(PortalNodes[originIndex]);
                if (integrationStart.index != -1)
                {
                    IntegrationStartIndicies.Add(integrationStart);
                }
            }
            PortalSequence.Add(originIndex);
            PortalTraversalData nextPortalData = PortalTraversalDataArray[originIndex];
            nextPortalData.mark |= PortalTraversalMark.Picked;
            PortalTraversalDataArray[originIndex] = nextPortalData;
            originIndex = nextPortalData.originIndex;
        }
        PortalSequence.Add(originIndex);
        if (!isIntegrationStartFound)
        {
            LocalIndex1d integrationStart = GetNotCalculatedIndexOfPortalNode(PortalNodes[originIndex]);
            if (integrationStart.index != -1)
            {
                IntegrationStartIndicies.Add(integrationStart); }
        }
        PortalSequenceBorders.Add(PortalSequence.Length);
    }
    int RunGraphWalkerFrom(int sourcePortalIndex, UnsafeHeap<int> traversalHeap, NativeArray<DijkstraTile> targetSectorCosts, ref UnsafeList<int> traversedIndicies)
    {
        NativeArray<PortalNode> portalNodes = PortalNodes;
        NativeArray<PortalTraversalData> portalTraversalDataArray = PortalTraversalDataArray;

        PortalTraversalData curData = PortalTraversalDataArray[sourcePortalIndex];
        if ((curData.mark & PortalTraversalMark.Picked) == PortalTraversalMark.Picked) { return -1; }

        //SET INNITIAL MARK
        curData = new PortalTraversalData()
        {
            fCost = 0f,
            gCost = 0f,
            hCost = 0f,
            mark = PortalTraversalMark.Picked | PortalTraversalMark.Included,
            originIndex = sourcePortalIndex,
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
        traversedIndicies.Add(curNodeIndex);
        TraverseNeighbours(curData, ref traversalHeap, ref traversedIndicies, targetSectorCosts, curNodeIndex, por1P2pIdx, por1P2pIdx + por1P2pCnt);
        TraverseNeighbours(curData, ref traversalHeap, ref traversedIndicies, targetSectorCosts, curNodeIndex, por2P2pIdx, por2P2pIdx + por2P2pCnt);
        SetNextNode();

        while ((curData.mark & PortalTraversalMark.Picked) != PortalTraversalMark.Picked)
        {
            TraverseNeighbours(curData, ref traversalHeap, ref traversedIndicies, targetSectorCosts, curNodeIndex, por1P2pIdx, por1P2pIdx + por1P2pCnt);
            TraverseNeighbours(curData, ref traversalHeap, ref traversedIndicies, targetSectorCosts, curNodeIndex, por2P2pIdx, por2P2pIdx + por2P2pCnt);
            SetNextNode();
        }
        return curNodeIndex;
        void SetNextNode()
        {
            if (traversalHeap.IsEmpty) { return; }
            int nextMinIndex = traversalHeap.ExtractMin();
            PortalTraversalData nextMinTraversalData = portalTraversalDataArray[nextMinIndex];
            while ((nextMinTraversalData.mark & PortalTraversalMark.Considered) == PortalTraversalMark.Considered)
            {
                nextMinIndex = traversalHeap.ExtractMin();
                nextMinTraversalData = portalTraversalDataArray[nextMinIndex];
            }
            nextMinTraversalData.mark |= PortalTraversalMark.Considered;
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
    void TraverseNeighbours(PortalTraversalData curData, ref UnsafeHeap<int> traversalHeap, ref UnsafeList<int> traversedIndicies, NativeArray<DijkstraTile> targetSectorCosts, int curNodeIndex, int from, int to)
    {
        for (int i = from; i < to; i++)
        {
            PortalToPortal neighbourConnection = PorPtrs[i];
            PortalNode portalNode = PortalNodes[neighbourConnection.Index];
            PortalTraversalData traversalData = PortalTraversalDataArray[neighbourConnection.Index];
            if ((traversalData.mark & PortalTraversalMark.Included) == PortalTraversalMark.Included)
            {
                float newGCost = curData.gCost + neighbourConnection.Distance;
                if (newGCost < traversalData.gCost)
                {
                    float newFCost = traversalData.hCost + newGCost;
                    traversalData.gCost = newGCost;
                    traversalData.fCost = newFCost;
                    traversalData.originIndex = curNodeIndex;
                    PortalTraversalDataArray[neighbourConnection.Index] = traversalData;
                    traversalHeap.Add(neighbourConnection.Index, traversalData.fCost, traversalData.hCost);
                }
            }
            else
            {
                float hCost = GetHCost(portalNode.Portal1.Index);
                float gCost = curData.gCost + neighbourConnection.Distance;
                float fCost = hCost + gCost;
                traversalData.hCost = hCost;
                traversalData.gCost = gCost;
                traversalData.fCost = fCost;
                traversalData.mark |= PortalTraversalMark.Included;
                traversalData.originIndex = curNodeIndex;
                PortalTraversalDataArray[neighbourConnection.Index] = traversalData;
                traversalHeap.Add(neighbourConnection.Index, traversalData.fCost, traversalData.hCost);
                traversedIndicies.Add(neighbourConnection.Index);
            }
        }
        if ((curData.mark & PortalTraversalMark.TargetNeighbour) == PortalTraversalMark.TargetNeighbour)
        {
            int targetNodeIndex = PortalNodes.Length - 1;
            PortalTraversalData traversalData = PortalTraversalDataArray[targetNodeIndex];
            if ((traversalData.mark & PortalTraversalMark.Included) == PortalTraversalMark.Included)
            {
                float newGCost = curData.gCost + GetGCostBetweenTargetAndTargetNeighbour(curNodeIndex, targetSectorCosts);
                if (newGCost < traversalData.gCost)
                {
                    float newFCost = traversalData.hCost + newGCost;
                    traversalData.gCost = newGCost;
                    traversalData.fCost = newFCost;
                    traversalData.originIndex = curNodeIndex;
                    PortalTraversalDataArray[targetNodeIndex] = traversalData;
                    traversalHeap.Add(targetNodeIndex, traversalData.fCost, traversalData.hCost);
                }
            }
            else
            {
                float hCost = 0f;
                float gCost = curData.gCost + GetGCostBetweenTargetAndTargetNeighbour(curNodeIndex, targetSectorCosts);
                float fCost = hCost + gCost;
                traversalData.hCost = hCost;
                traversalData.gCost = gCost;
                traversalData.fCost = fCost;
                traversalData.mark |= PortalTraversalMark.Included;
                traversalData.originIndex = curNodeIndex;
                PortalTraversalDataArray[targetNodeIndex] = traversalData;
                traversalHeap.Add(targetNodeIndex, traversalData.fCost, traversalData.hCost);
                traversedIndicies.Add(targetNodeIndex);
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
    float GetGCostBetweenTargetAndTargetNeighbour(int targetNeighbourIndex, NativeArray<DijkstraTile> targetSectorCosts)
    {
        int portalLocalIndexAtSector = GetPortalLocalIndexAtSector(PortalNodes[targetNeighbourIndex], _targetSectorIndex1d, _targetSectorStartIndex1d);
        return targetSectorCosts[portalLocalIndexAtSector].IntegratedCost;
    }
    UnsafeList<int> GetPortalIndicies(int targetSectorIndexF)
    {
        UnsafeList<int> portalIndicies = new UnsafeList<int>(0, Allocator.Temp);
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
                portalIndicies.Add(j + porPtr);
            }
        }
        return portalIndicies;
    }
    void PickSectorsFromPortalSequence()
    {
        int sectorTileAmount = SectorColAmount * SectorColAmount;
        int existingPickedFieldLength = ExistingFlowFieldLength;
        int newSectorCount = 0;

        for (int i = _newSequenceStartIndex; i < PortalSequence.Length; i++)
        {
            int portalIndex = PortalSequence[i];
            int windowIndex = PortalNodes[portalIndex].WinPtr;
            WindowNode windowNode = WindowNodes[windowIndex];
            int winToSecCnt = windowNode.WinToSecCnt;
            int winToSecPtr = windowNode.WinToSecPtr;
            for (int j = 0; j < winToSecCnt; j++)
            {
                int secPtr = WinToSecPtrs[j + winToSecPtr];
                if (SectorToPicked[secPtr] != 0) { continue; }
                SectorToPicked[secPtr] = existingPickedFieldLength + (newSectorCount * sectorTileAmount);
                PickedToSector.Add(secPtr);
                newSectorCount++;
            }
        }
        NewFlowFieldLength[0] = newSectorCount * sectorTileAmount + ExistingFlowFieldLength;
    }
    LocalIndex1d GetNotCalculatedIndexOfPortalNode(PortalNode portalNode)
    {
        int2 portal1General2d = new int2(portalNode.Portal1.Index.C, portalNode.Portal1.Index.R);
        int2 portal2General2d = new int2(portalNode.Portal2.Index.C, portalNode.Portal2.Index.R);
        int2 portal1Sector2d = portal1General2d / SectorColAmount;
        int2 portal2Sector2d = portal2General2d / SectorColAmount;
        int portal1Sector1d = portal1Sector2d.y * SectorMatrixColAmount + portal1Sector2d.x;
        int portal2Sector1d = portal2Sector2d.y * SectorMatrixColAmount + portal2Sector2d.x;
        int2 portal1SectorStart2d = portal1Sector2d * SectorColAmount;
        int2 portal2SectorStart2d = portal2Sector2d * SectorColAmount;
        int2 portal1Local2d = portal1General2d - portal1SectorStart2d;
        int2 portal2Local2d = portal2General2d - portal2SectorStart2d;
        int portal1Local1d = portal1Local2d.y * SectorColAmount + portal1Local2d.x;
        int portal2Local1d = portal2Local2d.y * SectorColAmount + portal2Local2d.x;
        if (SectorToPicked[portal1Sector1d] == 0 && SectorToPicked[portal2Sector1d] != 0)
        {
            return new LocalIndex1d(portal1Local1d, portal1Sector1d);
        }
        else if (SectorToPicked[portal2Sector1d] == 0 && SectorToPicked[portal1Sector1d] != 0)
        {
            return new LocalIndex1d(portal2Local1d, portal2Sector1d);
        }
        return new LocalIndex1d(-1, -1);
    }
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

    //HEAP
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
