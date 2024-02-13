using System;
using System.IO;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;


[BurstCompile]
internal struct PortalNodeAdditionTraversalJob : IJob
{
    internal int2 Target;
    internal int SectorColAmount;
    internal int AddedPortalSequenceBorderStartIndex;
    internal int SectorMatrixColAmount;
    internal int SectorMatrixRowAmount;
    internal int SectorTileAmount;
    internal int LOSRange;
    internal NativeArray<PortalTraversalData> PortalTraversalDataArray;
    internal NativeList<ActivePortal> PortalSequence;
    internal NativeList<int> PortalSequenceBorders;
    internal UnsafeList<PathSectorState> SectorStateTable;
    internal NativeList<int> PickedToSector;
    internal NativeReference<int> FlowFieldLength;

    [ReadOnly] internal NativeArray<SectorNode> SectorNodes;
    [ReadOnly] internal NativeArray<WindowNode> WindowNodes;
    [ReadOnly] internal NativeArray<int> WinToSecPtrs;
    [ReadOnly] internal NativeArray<PortalNode> PortalNodes;
    [ReadOnly] internal NativeArray<PortalToPortal> PorPtrs;
    [ReadOnly] internal NativeArray<UnsafeList<int>> IslandFields;
    [ReadOnly] internal NativeReference<int> NewPickedSectorStartIndex;

    internal NativeList<int> SourcePortalIndexList;
    internal NativeList<int> DijkstraStartIndicies;
    internal NativeReference<SectorsWihinLOSArgument> SectorWithinLOSState;

    public void Execute()
    {
        RunDijkstra();
        NativeArray<int> sourcePortalsAsArray = SourcePortalIndexList.AsArray();
        for (int i = 0; i < sourcePortalsAsArray.Length; i++)
        {
            PickPortalSequenceFromDijkstra(sourcePortalsAsArray[i]);
        }
        PickSectorsFromPortalSequence();

        int newAddedSectorStart = NewPickedSectorStartIndex.Value;
        int newAddedSectorCount = PickedToSector.Length - newAddedSectorStart;
        NativeSlice<int> newAddedSectors = new NativeSlice<int>(PickedToSector.AsArray(), newAddedSectorStart, newAddedSectorCount);
        if (ContainsSectorsWithinLOSRange(newAddedSectors))
        {
            SectorsWihinLOSArgument argument = SectorWithinLOSState.Value;
            argument |= SectorsWihinLOSArgument.AddedSectorWithinLOS;
            SectorWithinLOSState.Value = argument;
        }
    }
    void PickPortalSequenceFromDijkstra(int sourcePortal)
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
                NextIndex = math.select(PortalSequence.Length + 1, -1, curData.NextIndex == -1),
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
        NativeArray<PortalNode> portalNodes = PortalNodes;
        NativeArray<PortalToPortal> porPtrs = PorPtrs;

        for (int i = 0; i < DijkstraStartIndicies.Length; i++)
        {
            int index = DijkstraStartIndicies[i];
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
            if (travHeap.IsEmpty) { return -1; }
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
    void PickSectorsFromPortalSequence()
    {
        AddedPortalSequenceBorderStartIndex = math.select(AddedPortalSequenceBorderStartIndex - 1, 0, AddedPortalSequenceBorderStartIndex == 0);
        for (int i = AddedPortalSequenceBorderStartIndex; i < PortalSequenceBorders.Length - 1; i++)
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
            if (lastActivePortalInBorder.NextIndex != -1)
            {
                int portalIndex1 = lastActivePortalInBorder.Index;
                int portalIndex2 = PortalSequence[lastActivePortalInBorder.NextIndex].Index;
                PickSectorsBetweenportals(portalIndex1, portalIndex2);
            }
        }

        int sectorTileAmount = SectorColAmount * SectorColAmount;
        FlowFieldLength.Value = PickedToSector.Length * sectorTileAmount + 1;
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
        bool sector1Included = (SectorStateTable[win1Sec1Index] & PathSectorState.Included) == PathSectorState.Included;
        bool sector2Included = (SectorStateTable[win1Sec2Index] & PathSectorState.Included) == PathSectorState.Included;
        if ((win1Sec1Index == win2Sec1Index || win1Sec1Index == win2Sec2Index) && !sector1Included)
        {
            PickedToSector.Add(win1Sec1Index);
            SectorStateTable[win1Sec1Index] |= PathSectorState.Included;
        }
        if ((win1Sec2Index == win2Sec1Index || win1Sec2Index == win2Sec2Index) && !sector2Included)
        {
            PickedToSector.Add(win1Sec2Index);
            SectorStateTable[win1Sec2Index] |= PathSectorState.Included;

        }
    }
    bool ContainsSectorsWithinLOSRange(NativeSlice<int> sectors)
    {
        int losRange = LOSRange;
        int sectorColAmount = SectorColAmount;
        int sectorMatrixColAmount = SectorMatrixColAmount;
        int sectorMatrixRowAmount = SectorMatrixRowAmount;
        int sectorTileAmount = SectorTileAmount;

        int2 targetSector2d = FlowFieldUtilities.GetSector2D(Target, sectorColAmount);
        int extensionLength = losRange / sectorColAmount + math.select(0, 1, losRange % sectorColAmount > 0);
        int2 rangeTopRightSector = targetSector2d + new int2(extensionLength, extensionLength);
        int2 rangeBotLeftSector = targetSector2d - new int2(extensionLength, extensionLength);
        rangeTopRightSector = new int2()
        {
            x = math.select(rangeTopRightSector.x, sectorMatrixColAmount - 1, rangeTopRightSector.x >= sectorMatrixColAmount),
            y = math.select(rangeTopRightSector.y, sectorMatrixRowAmount - 1, rangeTopRightSector.y >= sectorMatrixRowAmount)
        };
        rangeBotLeftSector = new int2()
        {
            x = math.select(rangeBotLeftSector.x, 0, rangeBotLeftSector.x < 0),
            y = math.select(rangeBotLeftSector.y, 0, rangeBotLeftSector.y < 0)
        };
        for (int i = 0; i < sectors.Length; i++)
        {
            int sector1d = sectors[i];
            int sectorCol = sector1d % sectorMatrixColAmount;
            int sectorRow = sector1d / sectorMatrixColAmount;

            bool withinColRange = sectorCol >= rangeBotLeftSector.x && sectorCol <= rangeTopRightSector.x;
            bool withinRowRange = sectorRow >= rangeBotLeftSector.y && sectorRow <= rangeTopRightSector.y;
            if (withinColRange && withinRowRange) { return true; }
        }
        return false;
    }
    internal struct SingleUnsafeHeap<T> where T : unmanaged
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
        internal SingleUnsafeHeap(int size, Allocator allocator)
        {
            _array = new UnsafeList<HeapElement<T>>(size, allocator);
        }
        internal void Clear()
        {
            _array.Clear();
        }
        internal void Add(T element, float pri)
        {
            int elementIndex = _array.Length;
            _array.Add(new HeapElement<T>(element, pri));
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
        internal void SetPriority(int index, float pri)
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
        internal struct HeapElement<T> where T : unmanaged
        {
            internal T data;
            internal float pri;

            internal HeapElement(T data, float pri)
            {
                this.data = data;
                this.pri = pri;
            }
        }
    }
}