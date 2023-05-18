using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Analytics;
[BurstCompile]
public struct FieldGraphTraversalJob : IJob
{
    public Vector3 TargetPosition;
    //public NativeArray<Vector3> StartingPositions;
    public NativeArray<SectorNode> SectorNodes;
    public NativeArray<int> SecToWinPtrs;
    public NativeArray<WindowNode> WindowNodes;
    //public NativeArray<int> WinToSecPtrs;
    public NativeArray<PortalNode> PortalNodes;
    public NativeArray<PortalToPortal> PorPtrs;
    public NativeArray<byte> Costs;
    //public NativeArray<DirectionData> Directions;
    public NativeArray<LocalDirectionData> LocalDirections;
    public int FieldColAmount;
    public int FieldRowAmount;
    public float FieldTileSize;
    public int SectorTileAmount;
    public int SectorMatrixColAmount;
    //public int SectorMatrixRowAmount;
    //public int PortalPerWindow;
    //public NativeQueue<int> AStarQueue;
    //public NativeArray<int> DebugPortalIndicies;

    //Debug
    public NativeArray<int> ConnectionIndicies;
    public NativeArray<float> PortalDistances;

    int _targetSectorStartIndex;
    int _targetSectorIndex;
    public void Execute()
    {
        //DATA
        Index2 targetIndex = new Index2(Mathf.FloorToInt(TargetPosition.z / FieldTileSize), Mathf.FloorToInt(TargetPosition.x / FieldTileSize));
        Index2 targetSectorIndex = new Index2(targetIndex.R / SectorTileAmount, targetIndex.C / SectorTileAmount);
        int targetIndexFlat = targetIndex.R * FieldColAmount + targetIndex.C;
        _targetSectorIndex = targetSectorIndex.R * SectorMatrixColAmount + targetSectorIndex.C;
        Index2 targetSectorStartIndex = SectorNodes[_targetSectorIndex].Sector.StartIndex;
        _targetSectorStartIndex = targetSectorStartIndex.R * FieldColAmount + targetSectorStartIndex.C;


        //CODE
        NativeArray<AStarTile> integratedCosts = GetIntegratedCosts(targetIndexFlat);
        UnsafeList<int> targetPortalIndicies = GetTargetPortalIndicies(_targetSectorIndex);
        GetIntegratedPortalCosts(integratedCosts, targetPortalIndicies);
        
    }
    NativeArray<AStarTile> GetIntegratedCosts(int targetIndex)
    {
        NativeArray<AStarTile> integratedCosts = new NativeArray<AStarTile>(SectorTileAmount * SectorTileAmount, Allocator.Temp);
        NativeQueue<int> aStarQueue = new NativeQueue<int>(Allocator.Temp);
        CalculateIntegratedCosts(integratedCosts, aStarQueue, SectorNodes[_targetSectorIndex].Sector, targetIndex);
        return integratedCosts;
    }
    UnsafeList<int> GetTargetPortalIndicies(int targetSectorIndexF)
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

    //Graph A*

    UnsafeList<float> GetIntegratedPortalCosts(NativeArray<AStarTile> integratedCosts, UnsafeList<int> targetPortalIndicies)
    {
        //HELPER DATA
        NativeArray<PortalToPortal> porPtrs = PorPtrs;
        NativeArray<PortalNode> portalNodes = PortalNodes;
        UnsafeList<float> integratedPortalCosts = GetIntegratedCostBuffer();
        UnsafeList<bool> queueMark = new UnsafeList<bool>(portalNodes.Length, Allocator.Temp);
        queueMark.Length = portalNodes.Length;
        NativeQueue<int> traversalQueue = new NativeQueue<int>(Allocator.Temp);

        for (int i = 0; i < queueMark.Length; i++)
        {
            queueMark[i] = false;
        }

        //SETTING STARTING PORTALS
        for (int i = 0; i < targetPortalIndicies.Length; i++)
        {
            int portalNodeIndex = targetPortalIndicies[i];
            int portalLocalIndexAtSector = GetPortalLocalIndexAtSector(PortalNodes[portalNodeIndex], _targetSectorIndex, _targetSectorStartIndex);
            float integratedCost = integratedCosts[portalLocalIndexAtSector].IntegratedCost;
            if(integratedCost == float.MaxValue) { continue; }
            integratedPortalCosts[portalNodeIndex] = integratedCost;
            queueMark[portalNodeIndex] = true;
            ConnectionIndicies[portalNodeIndex] = portalNodeIndex;
            PortalDistances[portalNodeIndex] = integratedCost;
        }

        //ENQUEUE NEIGHBOURS OF STARTING PORTALS
        for(int i = 0; i < targetPortalIndicies.Length; i++)
        {
            PortalNode portalNode = portalNodes[targetPortalIndicies[i]];
            if (integratedPortalCosts[targetPortalIndicies[i]] == float.MaxValue) { continue; }
            EnqueueNeighbours(portalNode);
        }

        //REST OF THE PORTALS
        while (!traversalQueue.IsEmpty())
        {
            int pickedIndex = traversalQueue.Dequeue();
            PortalNode pickedPortalNode = PortalNodes[pickedIndex];
            IntFloat intf = GetMinCost(pickedPortalNode, pickedIndex);
            float minCost = intf.Float;
            int connectedIndex = intf.Int;
            integratedPortalCosts[pickedIndex] = minCost;
            EnqueueNeighbours(pickedPortalNode);
            ConnectionIndicies[pickedIndex] = connectedIndex;
            PortalDistances[pickedIndex] = minCost;
        }
        return integratedPortalCosts;

        UnsafeList<float> GetIntegratedCostBuffer()
        {
            UnsafeList<float> integratedPortalCosts = new UnsafeList<float>(portalNodes.Length, Allocator.Temp);
            integratedPortalCosts.Length = portalNodes.Length;
            for(int i = 0; i < integratedPortalCosts.Length; i++)
            {
                integratedPortalCosts[i] = float.MaxValue;
            }
            return integratedPortalCosts;
        }
        void EnqueueNeighbours(PortalNode portalNode)
        {
            int por1PorPtr = portalNode.Portal1.PorToPorPtr;
            int por1PorCnt = portalNode.Portal1.PorToPorCnt;
            int por2PorPtr = portalNode.Portal2.PorToPorPtr;
            int por2PorCnt = portalNode.Portal2.PorToPorCnt;

            for (int i = 0; i < por1PorCnt; i++)
            {
                int portalIndex = porPtrs[por1PorPtr + i].Index;
                if (queueMark[portalIndex]) { continue; }
                queueMark[portalIndex] = true;
                traversalQueue.Enqueue(portalIndex);
            }
            for (int i = 0; i < por2PorCnt; i++)
            {
                int portalIndex = porPtrs[por2PorPtr + i].Index;
                if (queueMark[portalIndex]) { continue; }
                queueMark[portalIndex] = true;
                traversalQueue.Enqueue(portalIndex);
            }
        }
        IntFloat GetMinCost(PortalNode portalNode, int selfIndex)
        {
            int por1PorPtr = portalNode.Portal1.PorToPorPtr;
            int por1PorCnt = portalNode.Portal1.PorToPorCnt;
            int por2PorPtr = portalNode.Portal2.PorToPorPtr;
            int por2PorCnt = portalNode.Portal2.PorToPorCnt;

            int index = 0;
            float minCost = float.MaxValue;
            for (int i = 0; i < por1PorCnt; i++)
            {
                //CALCULATE MIN COST
                PortalToPortal portopor = porPtrs[por1PorPtr + i];
                int porIndex = portopor.Index;
                float distance = portopor.Distance;
                float totalCost = distance + integratedPortalCosts[porIndex];
                if(totalCost < minCost) { minCost = totalCost; index = porIndex; }
            }
            for (int i = 0; i < por2PorCnt; i++)
            {
                //CALCULATE MIN COST
                PortalToPortal portopor = porPtrs[por2PorPtr + i];
                int porIndex = portopor.Index;
                float distance = portopor.Distance;
                float totalCost = distance + integratedPortalCosts[porIndex];
                if(totalCost < minCost) { minCost = totalCost; index = porIndex; }
            }

            return new IntFloat()
            {
                Int = index,
                Float = minCost
            };
        }
    }
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
    int GetGeneralIndex(int index, int sectorStartIndexF)
    {
        return sectorStartIndexF + (index / SectorTileAmount * FieldColAmount) + (index % SectorTileAmount);
    }
    int GetLocalIndex(int index, int sectorStartIndexF)
    {
        int distanceFromSectorStart = index - sectorStartIndexF;
        return (distanceFromSectorStart % FieldColAmount) + (SectorTileAmount * (distanceFromSectorStart / FieldColAmount));
    }

    //Tile A*
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
        void Enqueue(LocalDirectionData localDirections)
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
        float GetCost(LocalDirectionData localDirections)
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
}
struct IntFloat
{
    public int Int;
    public float Float;
}