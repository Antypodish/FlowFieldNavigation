using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Analytics;
using UnityEngine.Pool;

[BurstCompile]
public struct FieldGraphTraversalJob : IJob
{
    public NativeArray<Vector3> SourcePositions;
    public Vector3 TargetPosition;
    [ReadOnly] public NativeArray<SectorNode> SectorNodes;
    [ReadOnly] public NativeArray<int> SecToWinPtrs;
    [ReadOnly] public NativeArray<WindowNode> WindowNodes;
    [ReadOnly] public NativeArray<int> WinToSecPtrs;
    [ReadOnly] public NativeArray<PortalNode> PortalNodes;
    [ReadOnly] public NativeArray<PortalToPortal> PorPtrs;
    [ReadOnly] public NativeArray<byte> Costs;
    [ReadOnly] public NativeArray<LocalDirectionData> LocalDirections;
    public int FieldColAmount;
    public int FieldRowAmount;
    public float FieldTileSize;
    public int SectorTileAmount;
    public int SectorMatrixColAmount;
    public NativeArray<int> ConnectionIndicies;
    public NativeArray<float> PortalDistances;
    public NativeArray<PortalMark> PortalMarks;
    public NativeList<PortalSequence> PortalSequence;

    public NativeArray<int> SectorMarks;
    public NativeList<IntegrationFieldSector> IntegrationField;
    public NativeList<FlowFieldSector> FlowField;

    int _targetSectorStartIndex;
    int _targetSectorIndex;
    public void Execute()
    {
        //TARGET DATA
        Index2 targetIndex = new Index2(Mathf.FloorToInt(TargetPosition.z / FieldTileSize), Mathf.FloorToInt(TargetPosition.x / FieldTileSize));
        Index2 targetSectorIndex = new Index2(targetIndex.R / SectorTileAmount, targetIndex.C / SectorTileAmount);
        int targetIndexFlat = targetIndex.R * FieldColAmount + targetIndex.C;
        _targetSectorIndex = targetSectorIndex.R * SectorMatrixColAmount + targetSectorIndex.C;
        Index2 targetSectorStartIndex = SectorNodes[_targetSectorIndex].Sector.StartIndex;
        _targetSectorStartIndex = targetSectorStartIndex.R * FieldColAmount + targetSectorStartIndex.C;

        //ALGORITHM
        NativeArray<AStarTile> integratedCostsAtTargetSector = GetIntegratedCosts(targetIndexFlat);
        UnsafeList<int> targetPortalIndicies = GetPortalIndicies(_targetSectorIndex);
        SetPortalDistances(integratedCostsAtTargetSector, targetPortalIndicies);
        StartGraphWalker();
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

    //PORTAL GRAPH BFS
    void SetPortalDistances(NativeArray<AStarTile> integratedCosts, UnsafeList<int> targetPortalIndicies)
    {
        //HELPER DATA
        NativeArray<PortalToPortal> porPtrs = PorPtrs;
        NativeArray<PortalNode> portalNodes = PortalNodes;
        NativeArray<float> portalDistances = PortalDistances;
        ResetPortalDistances();
        NativeQueue<int> traversalQueue = new NativeQueue<int>(Allocator.Temp);
        NativeArray<PortalMark> portalMarks = PortalMarks;

        //SETTING STARTING PORTALS
        for (int i = 0; i < targetPortalIndicies.Length; i++)
        {
            int portalNodeIndex = targetPortalIndicies[i];
            int portalLocalIndexAtSector = GetPortalLocalIndexAtSector(PortalNodes[portalNodeIndex], _targetSectorIndex, _targetSectorStartIndex);
            float integratedCost = integratedCosts[portalLocalIndexAtSector].IntegratedCost;
            if(integratedCost == float.MaxValue) { continue; }
            portalDistances[portalNodeIndex] = integratedCost;
            portalMarks[portalNodeIndex] = PortalMark.BFS;
            ConnectionIndicies[portalNodeIndex] = portalNodeIndex;
        }

        //ENQUEUE NEIGHBOURS OF STARTING PORTALS
        for(int i = 0; i < targetPortalIndicies.Length; i++)
        {
            PortalNode portalNode = portalNodes[targetPortalIndicies[i]];
            if (portalDistances[targetPortalIndicies[i]] == float.MaxValue) { continue; }
            EnqueueNeighbours(portalNode);
        }

        //REST OF THE PORTALS
        while (!traversalQueue.IsEmpty())
        {
            int pickedIndex = traversalQueue.Dequeue();
            PortalNode pickedPortalNode = PortalNodes[pickedIndex];
            ConnectionAndCost intf = GetMinCost(pickedPortalNode, pickedIndex);
            float minCost = intf.Cost;
            int connectedIndex = intf.Connection;
            portalDistances[pickedIndex] = minCost;
            EnqueueNeighbours(pickedPortalNode);
            ConnectionIndicies[pickedIndex] = connectedIndex;
        }

        void ResetPortalDistances()
        {
            for(int i = 0; i < portalDistances.Length; i++)
            {
                portalDistances[i] = float.MaxValue;
            }
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
                if (portalMarks[portalIndex] == PortalMark.BFS) { continue; }
                portalMarks[portalIndex] = PortalMark.BFS;
                traversalQueue.Enqueue(portalIndex);
            }
            for (int i = 0; i < por2PorCnt; i++)
            {
                int portalIndex = porPtrs[por2PorPtr + i].Index;
                if (portalMarks[portalIndex] == PortalMark.BFS) { continue; }
                portalMarks[portalIndex] = PortalMark.BFS;
                traversalQueue.Enqueue(portalIndex);
            }
        }
        ConnectionAndCost GetMinCost(PortalNode portalNode, int selfIndex)
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
                float totalCost = distance + portalDistances[porIndex];
                if(totalCost < minCost) { minCost = totalCost; index = porIndex; }
            }
            for (int i = 0; i < por2PorCnt; i++)
            {
                //CALCULATE MIN COST
                PortalToPortal portopor = porPtrs[por2PorPtr + i];
                int porIndex = portopor.Index;
                float distance = portopor.Distance;
                float totalCost = distance + portalDistances[porIndex];
                if(totalCost < minCost) { minCost = totalCost; index = porIndex; }
            }

            return new ConnectionAndCost()
            {
                Connection = index,
                Cost = minCost
            };
        }
    }
    void SetPortalSequence(int startingPortalIndex)
    {
        NativeList<PortalSequence> portalSequence = PortalSequence;
        int portalIndex = startingPortalIndex;
        int connectionIndex = ConnectionIndicies[startingPortalIndex];
        while (true)
        {
            if (PortalMarks[portalIndex] == PortalMark.Walker)
            {
                break;
            }
            else if (connectionIndex == portalIndex)
            {
                PortalSequence porSeq = new PortalSequence()
                {
                    PortalPtr = portalIndex,
                    NextPortalPtrIndex = -1
                };
                portalSequence.Add(porSeq);
                PortalMarks[portalIndex] = PortalMark.Walker;
                break;
            }
            else if (PortalMarks[connectionIndex] == PortalMark.Walker)
            {
                PortalSequence porSeq = new PortalSequence()
                {
                    PortalPtr = portalIndex,
                    NextPortalPtrIndex = GetIndexOf(connectionIndex)
                };
                portalSequence.Add(porSeq);
                PortalMarks[portalIndex] = PortalMark.Walker;
                break;
            }
            else
            {
                PortalSequence porSeq = new PortalSequence()
                {
                    PortalPtr = portalIndex,
                    NextPortalPtrIndex = portalSequence.Length + 1
                };
                portalSequence.Add(porSeq);
                PortalMarks[portalIndex] = PortalMark.Walker;
                portalIndex = connectionIndex;
                connectionIndex = ConnectionIndicies[portalIndex];
            }

        }
        int GetIndexOf(int portalIndex)
        {
            for(int i = 0; i < portalSequence.Length; i++)
            {
                if (portalSequence[i].PortalPtr == portalIndex)
                {
                    return i;
                }
            }
            return -1;
        }
    }
    void PickSectorsFromPortalSequence()
    {
        for(int i = 0; i < PortalSequence.Length; i++)
        {
            int portalIndex = PortalSequence[i].PortalPtr;
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
    void StartGraphWalker()
    {
        for (int i = 0; i < SourcePositions.Length; i++)
        {
            Vector3 sourcePos = SourcePositions[i];
            Index2 sourceIndex = new Index2(Mathf.FloorToInt(sourcePos.z / FieldTileSize), Mathf.FloorToInt(sourcePos.x / FieldTileSize));
            Index2 sourceSectorIndex = new Index2(sourceIndex.R / SectorTileAmount, sourceIndex.C / SectorTileAmount);
            int sourceSectorIndexFlat = sourceSectorIndex.R * SectorMatrixColAmount + sourceSectorIndex.C;
            UnsafeList<int> sourcePortalIndicies = GetPortalIndicies(sourceSectorIndexFlat);

            for (int j = 0; j < sourcePortalIndicies.Length; j++)
            {
                SetPortalSequence(sourcePortalIndicies[j]);
            }
        }
        PickSectorsFromPortalSequence();
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
    int GetGeneralIndex(int index, int sectorStartIndexF)
    {
        return sectorStartIndexF + (index / SectorTileAmount * FieldColAmount) + (index % SectorTileAmount);
    }
    int GetLocalIndex(int index, int sectorStartIndexF)
    {
        int distanceFromSectorStart = index - sectorStartIndexF;
        return (distanceFromSectorStart % FieldColAmount) + (SectorTileAmount * (distanceFromSectorStart / FieldColAmount));
    }

    //SECTOR BFS
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
struct ConnectionAndCost
{
    public int Connection;
    public float Cost;
}
public enum PortalMark : byte
{
    None = 0,
    BFS = 1,
    Walker = 2,
};
public struct PortalSequence
{
    public int PortalPtr;
    public int NextPortalPtrIndex;
}