using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.XR;


[BurstCompile]
public struct PortalNodeTraversalJob : IJob
{
    public int2 TargetIndex;
    public int FieldColAmount;
    public int FieldRowAmount;
    public float FieldTileSize;
    public int SectorColAmount;
    public int SectorMatrixColAmount;

    public NativeArray<PortalTraversalData> PortalTraversalDataArray;
    public NativeList<ActivePortal> PortalSequence;

    public NativeList<int> PortalSequenceBorders;
    public UnsafeList<int> SectorToPicked;
    public UnsafeList<PathSectorState> SectorStateTable;
    public NativeList<int> PickedToSector;
    public NativeArray<DijkstraTile> TargetSectorCosts;
    public NativeArray<int> FlowFieldLength;
    public NativeList<int> SourcePortals;

    [ReadOnly] public NativeSlice<float2> SourcePositions;
    [ReadOnly] public UnsafeList<SectorNode> SectorNodes;
    [ReadOnly] public NativeArray<int> SecToWinPtrs;
    [ReadOnly] public NativeArray<WindowNode> WindowNodes;
    [ReadOnly] public NativeArray<int> WinToSecPtrs;
    [ReadOnly] public UnsafeList<PortalNode> PortalNodes;
    [ReadOnly] public NativeArray<PortalToPortal> PorPtrs;

    public NativeList<int> TargetNeighbourPortalIndicies;

    int _targetSectorIndex1d;
    public void Execute()
    {
        if (TargetNeighbourPortalIndicies.Length == 0)
        {
            return;
        }
        //TARGET DATA
        int2 targetSectorIndex2d = new int2(TargetIndex.x / SectorColAmount, TargetIndex.y / SectorColAmount);
        _targetSectorIndex1d = targetSectorIndex2d.y * SectorMatrixColAmount + targetSectorIndex2d.x;
        int2 _targetSectorStartIndex2d = targetSectorIndex2d * SectorColAmount;
        //START GRAPH WALKER
        PortalSequenceBorders.Add(0);
        RunDijkstra();
        NativeArray<int> sourcePortalsAsArray = SourcePortals;
        for(int  i = 0; i < sourcePortalsAsArray.Length; i++)
        {
            PickPortalSequenceFromFastMarching(sourcePortalsAsArray[i]);
        }
        PickSectorsFromPortalSequence();
        AddTargetSector();
    }
    void PickPortalSequenceFromFastMarching(int sourcePortal)
    {
        //NOTE: NextIndex of portalTraversalData is used as:
        //1. NextIndex in portalTraversalDataArray
        //2. PortalSequence of corresponding portalTraversalData
        //For memory optimization reasons :/

        PortalTraversalData sourceData = PortalTraversalDataArray[sourcePortal];

        if (sourceData.HasMark(PortalTraversalMark.DijkstraPicked)) { return; }

        int nextDataIndex = sourceData.NextIndex;
        sourceData.Mark |= PortalTraversalMark.DijkstraPicked;
        sourceData.NextIndex = PortalSequence.Length;
        PortalTraversalDataArray[sourcePortal] = sourceData;

        ActivePortal sourceActivePortal = new ActivePortal()
        {
            Index = sourcePortal,
            Distance = sourceData.DistanceFromTarget,
            NextIndex = PortalSequence.Length + 1,
        };
        
        //IF SOURCE IS TARGET NEIGHBOUR
        if (nextDataIndex == -1)
        {
            sourceActivePortal.NextIndex = -1;
            PortalSequence.Add(sourceActivePortal);
            PortalSequenceBorders.Add(PortalSequence.Length);
            return;
        }

        //IF SOURCE IS NOT TARGET NEIGHBOUR
        PortalSequence.Add(sourceActivePortal);
        int curIndex = nextDataIndex;

        while (curIndex != -1)
        {
            PortalTraversalData curData = PortalTraversalDataArray[curIndex];
            if (curData.HasMark(PortalTraversalMark.DijkstraPicked))
            {
                ActivePortal previousNode = PortalSequence[PortalSequence.Length - 1];
                previousNode.NextIndex = curData.NextIndex;
                PortalSequence[PortalSequence.Length - 1] = previousNode;
                break;
            }
            //PUSH ACTIVE PORTAL
            ActivePortal curActivePortal = new ActivePortal()
            {
                Index = curIndex,
                Distance = curData.DistanceFromTarget,
                NextIndex = math.select(PortalSequence.Length + 1, -1 , curData.NextIndex == -1),
            };
            PortalSequence.Add(curActivePortal);

            //MARK OR STOP
            int curDataIndex = curData.NextIndex;
            curData.Mark |= PortalTraversalMark.DijkstraPicked;
            curData.NextIndex = PortalSequence.Length - 1;
            PortalTraversalDataArray[curIndex] = curData;

            curIndex = curDataIndex;
        }
        PortalSequenceBorders.Add(PortalSequence.Length);
    }
    void RunDijkstra()
    {
        SingleUnsafeHeap<int> travHeap = new SingleUnsafeHeap<int>(0, Allocator.Temp);
        NativeArray<PortalTraversalData> portalTraversalDataArray = PortalTraversalDataArray;
        UnsafeList<PortalNode> portalNodes = PortalNodes;
        NativeArray<PortalToPortal> porPtrs = PorPtrs;

        //MARK TARGET NEIGHBOURS
        for (int i = 0; i < TargetNeighbourPortalIndicies.Length; i++)
        {
            int index = TargetNeighbourPortalIndicies[i];
            PortalTraversalData targetNeighbour = PortalTraversalDataArray[index];
            targetNeighbour.Mark |= PortalTraversalMark.DijkstraTraversed;
            PortalTraversalDataArray[index] = targetNeighbour;
        }

        for (int i = 0; i < TargetNeighbourPortalIndicies.Length; i++)
        {
            int index = TargetNeighbourPortalIndicies[i];
            float distanceFromTarget = portalTraversalDataArray[index].DistanceFromTarget;
            EnqueueNeighbours(index, distanceFromTarget);
        }

        int curIndex = GetNextIndex();
        while (curIndex != -1)
        {
            float distanceFromTarget = portalTraversalDataArray[curIndex].DistanceFromTarget;
            EnqueueNeighbours(curIndex, distanceFromTarget);
            curIndex = GetNextIndex();
        }
        int GetNextIndex()
        {
            if (travHeap.IsEmpty) { return - 1; }
            int nextMinIndex = travHeap.ExtractMin();
            PortalTraversalData nextMinTraversalData = portalTraversalDataArray[nextMinIndex];
            while (nextMinTraversalData.HasMark(PortalTraversalMark.DijstraExtracted))
            {
                if (travHeap.IsEmpty) { return -1; }
                nextMinIndex = travHeap.ExtractMin();
                nextMinTraversalData = portalTraversalDataArray[nextMinIndex];
            }
            nextMinTraversalData.Mark |= PortalTraversalMark.DijstraExtracted;
            portalTraversalDataArray[nextMinIndex] = nextMinTraversalData;
            return nextMinIndex;
        }
        void EnqueueNeighbours(int index, float gCost)
        {
            PortalNode portal = portalNodes[index];
            int por1Ptr = portal.Portal1.PorToPorPtr;
            int por1Cnt = portal.Portal1.PorToPorCnt;
            int por2Ptr = portal.Portal2.PorToPorPtr;
            int por2Cnt = portal.Portal2.PorToPorCnt;

            for (int i = por1Ptr; i < por1Ptr + por1Cnt; i++)
            {
                int portalIndex = porPtrs[i].Index;
                float portalDistance = porPtrs[i].Distance;
                PortalTraversalData porData = portalTraversalDataArray[portalIndex];
                if (!porData.HasMark(PortalTraversalMark.Reduced) || porData.HasMark(PortalTraversalMark.DijstraExtracted)) { continue; }
                if (porData.HasMark(PortalTraversalMark.DijkstraTraversed))
                {
                    float newGCost = gCost + portalDistance + 1;
                    if(porData.DistanceFromTarget <= newGCost) { continue; }
                    porData.DistanceFromTarget = newGCost;
                    porData.NextIndex = index;
                    portalTraversalDataArray[portalIndex] = porData;
                    travHeap.Add(portalIndex, newGCost);
                    continue;
                }
                porData.Mark |= PortalTraversalMark.DijkstraTraversed;
                porData.DistanceFromTarget = gCost + portalDistance + 1;
                porData.NextIndex = index;
                portalTraversalDataArray[portalIndex] = porData;
                travHeap.Add(portalIndex, gCost + portalDistance + 1);
            }
            for (int i = por2Ptr; i < por2Ptr + por2Cnt; i++)
            {
                int portalIndex = porPtrs[i].Index;
                float portalDistance = porPtrs[i].Distance;
                PortalTraversalData porData = portalTraversalDataArray[portalIndex];
                if (!porData.HasMark(PortalTraversalMark.Reduced) || porData.HasMark(PortalTraversalMark.DijstraExtracted)) { continue; }
                if (porData.HasMark(PortalTraversalMark.DijkstraTraversed))
                {
                    float newGCost = gCost + portalDistance + 1;
                    if (porData.DistanceFromTarget <= newGCost) { continue; }
                    porData.DistanceFromTarget = newGCost;
                    porData.NextIndex = index;
                    portalTraversalDataArray[portalIndex] = porData;
                    travHeap.Add(portalIndex, newGCost);
                    continue;
                }
                porData.Mark |= PortalTraversalMark.DijkstraTraversed;
                porData.DistanceFromTarget = gCost + portalDistance + 1;
                porData.NextIndex = index;
                portalTraversalDataArray[portalIndex] = porData;
                travHeap.Add(portalIndex, gCost + portalDistance + 1);
            }
        }
    }
    void RunFastMarching()
    {/*
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
        }*/
    }
    void PickSectorsFromPortalSequence()
    {
        for (int i = 0; i < PortalSequenceBorders.Length - 1; i++)
        {
            int start = PortalSequenceBorders[i];
            int end = PortalSequenceBorders[i + 1];
            for (int j = start; j < end - 1; j++)
            {
                int portalIndex1 = PortalSequence[j].Index;
                int portalIndex2 = PortalSequence[j + 1].Index;
                PickSectorsBetweenportals(portalIndex1, portalIndex2);
            }
            ActivePortal lastActivePortalInBorder = PortalSequence[end - 1];
            if(lastActivePortalInBorder.NextIndex != -1)
            {
                int portalIndex1 = lastActivePortalInBorder.Index;
                int portalIndex2 = PortalSequence[lastActivePortalInBorder.NextIndex].Index;
                PickSectorsBetweenportals(portalIndex1, portalIndex2);
            }

        }
    }
    void PickSectorsBetweenportals(int portalIndex1, int portalIndex2)
    {
        int sectorTileAmount = SectorColAmount * SectorColAmount;
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
            SectorStateTable[win1Sec1Index] |= PathSectorState.Included;
        }
        if ((win1Sec2Index == win2Sec1Index || win1Sec2Index == win2Sec2Index) && SectorToPicked[win1Sec2Index] == 0)
        {
            SectorToPicked[win1Sec2Index] = PickedToSector.Length * sectorTileAmount + 1;
            PickedToSector.Add(win1Sec2Index);
            SectorStateTable[win1Sec2Index] |= PathSectorState.Included;
        }
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
        FlowFieldLength[0] = PickedToSector.Length * sectorTileAmount + 1;
    }
    private struct SingleUnsafeHeap<T> where T : unmanaged
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
        public SingleUnsafeHeap(int size, Allocator allocator)
        {
            _array = new UnsafeList<HeapElement<T>>(size, allocator);
        }
        public void Clear()
        {
            _array.Clear();
        }
        public void Add(T element, float pri)
        {
            int elementIndex = _array.Length;
            _array.Add(new HeapElement<T>(element, pri));
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
        public void SetPriority(int index, float pri)
        {
            int length = _array.Length;
            HeapElement<T> cur = _array[index];
            cur.pri = pri;
            _array[index] = cur;
            int parIndex = index / 2 - 1;
            int lcIndex = index * 2 + 1;
            int rcIndex = index * 2 + 2;
            parIndex = math.select(index, parIndex, parIndex >= 0);
            lcIndex = math.select(index, lcIndex, lcIndex < length);
            rcIndex = math.select(index, rcIndex, rcIndex < length);
            HeapElement<T> parent = _array[parIndex];
            if (cur.pri < parent.pri)
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
            bool isCurSmaller = cur.pri < par.pri;
            while (isCurSmaller)
            {
                _array[parIndex] = cur;
                _array[curIndex] = par;
                curIndex = parIndex;
                parIndex = math.select((curIndex - 1) / 2, 0, curIndex == 0);
                par = _array[parIndex];
                isCurSmaller = cur.pri < par.pri;
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
                bool lcSmallerThanRc = lc.pri < rc.pri;
                bool lcSmallerThanCur = lc.pri < cur.pri;
                bool rcSmallerThanCur = rc.pri < cur.pri;

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
            public float pri;

            public HeapElement(T data, float pri)
            {
                this.data = data;
                this.pri = pri;
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
[BurstCompile]
public struct PortalTraversalData
{
    public int OriginIndex;
    public int NextIndex;
    public float GCost;
    public float HCost;
    public float FCost;
    public float DistanceFromTarget;
    public PortalTraversalMark Mark;
    public bool HasMark(PortalTraversalMark mark)
    {
        return (Mark & mark) == mark;
    }
}
public struct DijkstraTile
{
    public byte Cost;
    public float IntegratedCost;
    public bool IsAvailable;

    public DijkstraTile(byte cost, float integratedCost, bool isAvailable)
    {
        Cost = cost;
        IntegratedCost = integratedCost;
        IsAvailable = isAvailable;
    }
}
[Flags]
public enum PortalTraversalMark : byte
{
    AStarTraversed = 1,
    AStarExtracted = 2,
    AStarPicked = 4,
    DijkstraTraversed = 8,
    DijkstraPicked = 16,
    DijstraExtracted = 32,
    TargetNeighbour = 64,
    Reduced = 128,
}