using JetBrains.Annotations;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Analytics;
using UnityEngine.Pool;

[BurstCompile]
public struct FieldGraphTraversalJob : IJob
{
    public Vector2 TargetPosition;
    public int FieldColAmount;
    public int FieldRowAmount;
    public float FieldTileSize;
    public int SectorTileAmount;
    public int SectorMatrixColAmount;

    public NativeArray<Vector3> SourcePositions;
    public NativeArray<PortalTraversalData> PortalTraversalDataArray;
    public NativeList<int> PortalSequence;
    public NativeArray<int> SectorMarks;
    public NativeList<IntegrationFieldSector> IntegrationField;
    public NativeList<FlowFieldSector> FlowField;

    [ReadOnly] public NativeArray<SectorNode> SectorNodes;
    [ReadOnly] public NativeArray<int> SecToWinPtrs;
    [ReadOnly] public NativeArray<WindowNode> WindowNodes;
    [ReadOnly] public NativeArray<int> WinToSecPtrs;
    [ReadOnly] public NativeArray<PortalNode> PortalNodes;
    [ReadOnly] public NativeArray<PortalToPortal> PorPtrs;
    [ReadOnly] public NativeArray<byte> Costs;
    [ReadOnly] public NativeArray<SectorDirectionData> LocalDirections;

    Index2 _targetIndex;
    int _targetSectorStartIndex;
    int _targetSectorIndex;
    public void Execute()
    {
        //TARGET DATA
        _targetIndex = new Index2(Mathf.FloorToInt(TargetPosition.y / FieldTileSize), Mathf.FloorToInt(TargetPosition.x / FieldTileSize));
        Index2 targetSectorIndex = new Index2(_targetIndex.R / SectorTileAmount, _targetIndex.C / SectorTileAmount);
        int targetIndexFlat = _targetIndex.R * FieldColAmount + _targetIndex.C;
        _targetSectorIndex = targetSectorIndex.R * SectorMatrixColAmount + targetSectorIndex.C;
        Index2 targetSectorStartIndex = SectorNodes[_targetSectorIndex].Sector.StartIndex;
        _targetSectorStartIndex = targetSectorStartIndex.R * FieldColAmount + targetSectorStartIndex.C;

        NativeArray<AStarTile> integratedCostsAtTargetSector = GetIntegratedCosts(targetIndexFlat);
        UnsafeList<int> targetSectorPortalIndicies = GetPortalIndicies(_targetSectorIndex); 
        
        //SET TARGET PORTAL DATA
        PortalTraversalDataArray[PortalTraversalDataArray.Length - 1] = new PortalTraversalData()
        {
            originIndex = PortalTraversalDataArray.Length - 1,
            hCost = float.MaxValue,
            gCost = float.MaxValue,
            fCost = float.MaxValue,
            mark = PortalTraversalMark.Picked,
        };

        //SET TARGET NEIGHBOUR DATA
        SetTargetNeighbourPortalData(targetSectorPortalIndicies, integratedCostsAtTargetSector);

        //START GRAPH WALKER
        UnsafeList<int> traversedIndicies = new UnsafeList<int>(10, Allocator.Temp);
        JobHeap<int> walkerHeap = new JobHeap<int>(10000, Allocator.Temp);
        for (int i = 0; i < SourcePositions.Length; i++)
        {
            Vector3 sourcePos = SourcePositions[i];
            Index2 sourceIndex = new Index2(Mathf.FloorToInt(sourcePos.z / FieldTileSize), Mathf.FloorToInt(sourcePos.x / FieldTileSize));
            Index2 sourceSectorIndex = new Index2(sourceIndex.R / SectorTileAmount, sourceIndex.C / SectorTileAmount);
            int sourceSectorIndexFlat = sourceSectorIndex.R * SectorMatrixColAmount + sourceSectorIndex.C;
            UnsafeList<int> sourcePortalIndicies = GetPortalIndicies(sourceSectorIndexFlat);
            for(int j = 0; j < sourcePortalIndicies.Length; j++)
            {
                RunGraphWalkerFrom(sourcePortalIndicies[j], walkerHeap, integratedCostsAtTargetSector, ref traversedIndicies);
                SetPortalSequence(sourcePortalIndicies[j]);
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
    }
    void SetPortalSequence(int sourceNodeIndex)
    {
        int travArrayLength = PortalTraversalDataArray.Length;
        int originIndex = travArrayLength - 1;
        while (originIndex != sourceNodeIndex)
        {
            PortalSequence.Add(originIndex);
            PortalTraversalData nextPortalData = PortalTraversalDataArray[originIndex];
            nextPortalData.mark |= PortalTraversalMark.Picked;
            PortalTraversalDataArray[originIndex] = nextPortalData;
            originIndex = nextPortalData.originIndex;
        }
        PortalSequence.Add(originIndex);
    }
    void RunGraphWalkerFrom(int sourcePortalIndex, JobHeap<int> traversalHeap, NativeArray<AStarTile> targetSectorCosts, ref UnsafeList<int> traversedIndicies)
    {
        NativeArray<PortalNode> portalNodes = PortalNodes;
        NativeArray<PortalTraversalData> portalTraversalDataArray = PortalTraversalDataArray;

        PortalTraversalData curData = PortalTraversalDataArray[sourcePortalIndex];
        if((curData.mark & PortalTraversalMark.Picked) == PortalTraversalMark.Picked) { return; }
        
        //SET INNITIAL MARK
        curData = new PortalTraversalData()
        {
            fCost = 0f,
            gCost = 0f,
            hCost = 0f,
            mark = curData.mark | PortalTraversalMark.Picked | PortalTraversalMark.Included,
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
        TraverseNeighbours(curData, ref traversalHeap, ref traversedIndicies, targetSectorCosts, curNodeIndex, por1P2pIdx, por1P2pIdx + por1P2pCnt);
        TraverseNeighbours(curData, ref traversalHeap, ref traversedIndicies, targetSectorCosts, curNodeIndex, por2P2pIdx, por2P2pIdx + por2P2pCnt);
        SetNextNode();
        traversedIndicies.Add(curNodeIndex);
        while ((curData.mark & PortalTraversalMark.Picked) != PortalTraversalMark.Picked)
        {
            TraverseNeighbours(curData, ref traversalHeap, ref traversedIndicies, targetSectorCosts, curNodeIndex, por1P2pIdx, por1P2pIdx + por1P2pCnt);
            TraverseNeighbours(curData, ref traversalHeap, ref traversedIndicies, targetSectorCosts, curNodeIndex, por2P2pIdx, por2P2pIdx + por2P2pCnt);
            SetNextNode();
        }

        void SetNextNode()
        {
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
    void TraverseNeighbours(PortalTraversalData curData, ref JobHeap<int> traversalHeap, ref UnsafeList<int> traversedIndicies, NativeArray<AStarTile> targetSectorCosts, int curNodeIndex, int from, int to)
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
        Index2 targetPos = _targetIndex;
        int xDif = math.abs(nodePos.C - targetPos.C);
        int yDif = math.abs(nodePos.R - targetPos.R);
        int smallOne = math.min(xDif, yDif);
        int bigOne = math.max(xDif, yDif);
        return (bigOne - smallOne) * 1f + smallOne * 1.4f;
    }
    void SetTargetNeighbourPortalData(UnsafeList<int> targetSectorPortalIndicies, NativeArray<AStarTile> integratedCostsAtTargetSector)
    {
        for (int i = 0; i < targetSectorPortalIndicies.Length; i++)
        {
            int portalNodeIndex = targetSectorPortalIndicies[i];
            int portalLocalIndexAtSector = GetPortalLocalIndexAtSector(PortalNodes[portalNodeIndex], _targetSectorIndex, _targetSectorStartIndex);
            float integratedCost = integratedCostsAtTargetSector[portalLocalIndexAtSector].IntegratedCost;
            if (integratedCost == float.MaxValue) { continue; }
            PortalTraversalDataArray[portalNodeIndex] = new PortalTraversalData()
            {
                fCost = float.MaxValue,
                gCost = float.MaxValue,
                hCost = float.MaxValue,
                originIndex = portalNodeIndex,
                mark = PortalTraversalMark.TargetNeighbour,
            };
        }
    }
    float GetGCostBetweenTargetAndTargetNeighbour(int targetNeighbourIndex, NativeArray<AStarTile> targetSectorCosts)
    {
        int portalLocalIndexAtSector = GetPortalLocalIndexAtSector(PortalNodes[targetNeighbourIndex], _targetSectorIndex, _targetSectorStartIndex);
        return targetSectorCosts[portalLocalIndexAtSector].IntegratedCost;
    }
    NativeArray<AStarTile> GetIntegratedCosts(int targetIndex)
    {
        NativeArray<AStarTile> integratedCosts = new NativeArray<AStarTile>(SectorTileAmount * SectorTileAmount, Allocator.Temp);
        NativeQueue<int> aStarQueue = new NativeQueue<int>(Allocator.Temp);
        CalculateIntegratedCosts(integratedCosts, aStarQueue, SectorNodes[_targetSectorIndex].Sector, targetIndex);
        return integratedCosts;
    }
    UnsafeList<int> GetPortalIndicies(int targetSectorIndexF)
    {
        UnsafeList<int> portalIndicies = new UnsafeList<int>(0,Allocator.Temp);
        SectorNode sectorNode = SectorNodes[targetSectorIndexF];
        int winPtr = sectorNode.SecToWinPtr;
        int winCnt = sectorNode.SecToWinCnt;
        for(int i = 0; i < winCnt; i++)
        {
            WindowNode windowNode = WindowNodes[SecToWinPtrs[winPtr + i]];
            int porPtr = windowNode.PorPtr;
            int porCnt = windowNode.PorCnt;
            for(int j = 0; j < porCnt; j++)
            {
                portalIndicies.Add(j + porPtr);
            }
        }
        return portalIndicies;
    }
    void PickSectorsFromPortalSequence()
    {
        for(int i = 0; i < PortalSequence.Length; i++)
        {
            int portalIndex = PortalSequence[i];
            int windowIndex = PortalNodes[portalIndex].WinPtr;
            WindowNode windowNode = WindowNodes[windowIndex];
            int winToSecCnt = windowNode.WinToSecCnt;
            int winToSecPtr = windowNode.WinToSecPtr;
            for(int j = 0; j < winToSecCnt; j++)
            {
                int secPtr = WinToSecPtrs[j + winToSecPtr];
                if (SectorMarks[secPtr] != 0) { continue; }
                SectorMarks[secPtr] = IntegrationField.Length;
                IntegrationField.Add(new IntegrationFieldSector(secPtr));
                FlowField.Add(new FlowFieldSector(secPtr));
            }
        }
    }

    //HELPERS
    int GetPortalLocalIndexAtSector(PortalNode portalNode, int sectorIndex, int sectorStartIndex)
    {
        Index2 index1 = portalNode.Portal1.Index;
        int index1Flat = index1.R * FieldColAmount + index1.C;
        int index1SectorIndex = (index1.R / SectorTileAmount) * SectorMatrixColAmount + (index1.C / SectorTileAmount);
        if(sectorIndex == index1SectorIndex)
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
        return (distanceFromSectorStart % FieldColAmount) + (SectorTileAmount * (distanceFromSectorStart / FieldColAmount));
    }

    //TARGET SECTOR COST CALCULATION
    void CalculateIntegratedCosts(NativeArray<AStarTile> integratedCosts, NativeQueue<int> aStarQueue, Sector sector, int targetIndexFlat)
    {
        //DATA
        int sectorTileAmount = SectorTileAmount;
        int fieldColAmount = FieldColAmount;
        int sectorStartIndexFlat = sector.StartIndex.R * fieldColAmount + sector.StartIndex.C;
        NativeArray<byte> costs = Costs;

        //CODE

        Reset();
        int targetLocalIndex = GetLocalIndex(targetIndexFlat);
        AStarTile targetTile = integratedCosts[targetLocalIndex];
        targetTile.IntegratedCost = 0f;
        targetTile.IsAvailable = false;
        integratedCosts[targetLocalIndex] = targetTile;
        Enqueue(LocalDirections[targetLocalIndex]);
        while (!aStarQueue.IsEmpty())
        {
            int localindex = aStarQueue.Dequeue();
            AStarTile tile = integratedCosts[localindex];
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
                if(cost == byte.MaxValue)
                {
                    integratedCosts[i] = new AStarTile(cost, float.MaxValue, false);
                    continue;
                }
                integratedCosts[i] = new AStarTile(cost, float.MaxValue, true);
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
                AStarTile tile = integratedCosts[n];
                tile.IsAvailable = false;
                integratedCosts[n] = tile;
            }
            if (integratedCosts[e].IsAvailable)
            {
                aStarQueue.Enqueue(e);
                AStarTile tile = integratedCosts[e];
                tile.IsAvailable = false;
                integratedCosts[e] = tile;
            }
            if (integratedCosts[s].IsAvailable)
            {
                aStarQueue.Enqueue(s);
                AStarTile tile = integratedCosts[s];
                tile.IsAvailable = false;
                integratedCosts[s] = tile;
            }
            if (integratedCosts[w].IsAvailable)
            {
                aStarQueue.Enqueue(w);
                AStarTile tile = integratedCosts[w];
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
            return sectorStartIndexFlat + (index / sectorTileAmount * fieldColAmount) +(index % sectorTileAmount);
        }
        int GetLocalIndex(int index)
        {
            int distanceFromSectorStart = index - sectorStartIndexFlat;
            return (distanceFromSectorStart % fieldColAmount) + (sectorTileAmount * (distanceFromSectorStart / fieldColAmount));
        }
    }
    struct AStarTile
    {
        public byte Cost;
        public float IntegratedCost;
        public bool IsAvailable;

        public AStarTile(byte cost, float integratedCost, bool isAvailable)
        {
            Cost = cost;
            IntegratedCost = integratedCost;
            IsAvailable = isAvailable;
        }
    }

    //HEAP
    public struct JobHeap<T> where T : unmanaged
    {
        public UnsafeList<HeapElement<T>> _array;
        public T this[int index]
        {
            get
            {
                return _array[index].data;
            }
        }
        public JobHeap(int size, Allocator allocator)
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
//NEW
public struct PortalTraversalData
{
    public int originIndex;
    public float gCost;
    public float hCost;
    public float fCost;
    public PortalTraversalMark mark;
}
[Flags]
public enum PortalTraversalMark : byte
{
    None = 0,
    Included = 1,
    Considered = 2,
    Picked = 4,
    TargetNeighbour = 8,
}



//OLD
struct ConnectionAndCost
{
    public int Connection;
    public float Cost;
}
public enum PortalMark : byte
{
    None = 0,
    BFS = 1,
    MainWalker = 2,
    SideWalker = 3,
};
public struct PortalSequence
{
    public int PortalPtr;
    public int NextPortalPtrIndex;
}