using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;

[BurstCompile]
public struct FlowFieldJob : IJobParallelFor
{
    public int SectorIndex1d;
    public int SectorMatrixColAmount;
    public int SectorMatrixRowAmount;
    public int SectorColAmount;
    public int SectorRowAmount;
    [ReadOnly] public NativeArray<int> SectorMarks;
    [ReadOnly] public NativeArray<UnsafeList<LocalDirectionData1d>> Directions;
    [ReadOnly] public NativeList<IntegrationFieldSector> IntegrationField;
    public UnsafeList<FlowData> FlowSector;

    public void Execute(int index)
    {
        if (IntegrationField[SectorMarks[SectorIndex1d]].integrationSector[index].Mark == IntegrationMark.LOSPass) { FlowSector[index] = FlowData.LOS; return; }

        //DATA
        NativeList<IntegrationFieldSector> integrationField = IntegrationField;
        NativeArray<UnsafeList<LocalDirectionData1d>> directions = Directions;
        NativeArray<int> sectorMarks = SectorMarks;
        int pickedSector1d = SectorIndex1d;
        int sectorMatrixColAmount = SectorMatrixColAmount;
        int sectorMatrixRowAmount = SectorMatrixRowAmount;
        int sectorMatrixTileAmount = sectorMatrixColAmount * sectorMatrixRowAmount;
        int sectorColAmount = SectorColAmount;
        int sectorTileAmount = SectorColAmount * SectorRowAmount;

        //////////LOOKUP TABLE////////////
        //////////////////////////////////
        LocalDirectionData1d direction;
        float nIntCost;
        float eIntCost;
        float sIntCost;
        float wIntCost;
        float neIntCost;
        float seIntCost;
        float swIntCost;
        float nwIntCost;
        UnsafeList<IntegrationTile> nSector;
        UnsafeList<IntegrationTile> eSector;
        UnsafeList<IntegrationTile> sSector;
        UnsafeList<IntegrationTile> wSector;
        UnsafeList<IntegrationTile> neSector;
        UnsafeList<IntegrationTile> seSector;
        UnsafeList<IntegrationTile> swSector;
        UnsafeList<IntegrationTile> nwSector;
        /////////////////////////////////////////

        SetLookupTable();

        float minCost = float.MaxValue;
        FlowData minFlow = FlowData.None;
        if(nIntCost < minCost) { minCost = nIntCost; minFlow = FlowData.N; }
        if(eIntCost < minCost) { minCost = eIntCost; minFlow = FlowData.E; }
        if(sIntCost < minCost) { minCost = sIntCost; minFlow = FlowData.S; }
        if(wIntCost < minCost) { minCost = wIntCost; minFlow = FlowData.W; }
        if(neIntCost < minCost) { minCost = neIntCost; minFlow = FlowData.NE; }
        if(seIntCost < minCost) { minCost = seIntCost; minFlow = FlowData.SE; }
        if(swIntCost < minCost) { minCost = swIntCost; minFlow = FlowData.SW; }
        if(nwIntCost < minCost) { minCost = nwIntCost; minFlow = FlowData.NW; }
        FlowSector[index] = minFlow;

        void SetLookupTable()
        {
            direction = directions[pickedSector1d][index];
            //SECTOR MARKS
            int nSectorMark = sectorMarks[direction.nSector];
            int eSectorMark = sectorMarks[direction.eSector];
            int sSectorMark = sectorMarks[direction.sSector];
            int wSectorMark = sectorMarks[direction.wSector];
            int neSectorMark = sectorMarks[direction.neSector];
            int seSectorMark = sectorMarks[direction.seSector];
            int swSectorMark = sectorMarks[direction.swSector];
            int nwSectorMark = sectorMarks[direction.nwSector];

            //INTEGRATED COSTS
            nIntCost = float.MaxValue;
            eIntCost = float.MaxValue;
            sIntCost = float.MaxValue;
            wIntCost = float.MaxValue;
            neIntCost = float.MaxValue;
            seIntCost = float.MaxValue;
            swIntCost = float.MaxValue;
            nwIntCost = float.MaxValue;

            nSector = integrationField[nSectorMark].integrationSector;
            eSector = integrationField[eSectorMark].integrationSector;
            sSector = integrationField[sSectorMark].integrationSector;
            wSector = integrationField[wSectorMark].integrationSector;
            neSector = integrationField[neSectorMark].integrationSector;
            seSector = integrationField[seSectorMark].integrationSector;
            swSector = integrationField[swSectorMark].integrationSector;
            nwSector = integrationField[nwSectorMark].integrationSector;

            if (nSectorMark != 0) { nIntCost = nSector[direction.n].Cost; }
            if (eSectorMark != 0) { eIntCost = eSector[direction.e].Cost; }
            if (sSectorMark != 0) { sIntCost = sSector[direction.s].Cost; }
            if (wSectorMark != 0) { wIntCost = wSector[direction.w].Cost; }
            if (neSectorMark != 0) { neIntCost = neSector[direction.ne].Cost; }
            if (seSectorMark != 0) { seIntCost = seSector[direction.se].Cost; }
            if (swSectorMark != 0) { swIntCost = swSector[direction.sw].Cost; }
            if (nwSectorMark != 0) { nwIntCost = nwSector[direction.nw].Cost; }
        }
    }
}
public enum FlowData : byte
{
    None = 0,
    N = 1,
    NE = 2,
    E = 3,
    SE = 4,
    S = 5,
    SW = 6,
    W = 7,
    NW = 8,
    LOS,
}