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
    [ReadOnly] public NativeList<IntegrationFieldSector> IntegrationField;
    public UnsafeList<FlowData> FlowSector;

    public void Execute(int index)
    {
        if (IntegrationField[SectorMarks[SectorIndex1d]].integrationSector[index].Mark == IntegrationMark.LOSPass)
        {
            FlowData flow = FlowData.LOS;
            FlowSector[index] = flow;
            return;
        }

        //DATA
        NativeList<IntegrationFieldSector> integrationField = IntegrationField;
        int pickedSector1d = SectorIndex1d;
        int sectorMatrixColAmount = SectorMatrixColAmount;
        int sectorMatrixRowAmount = SectorMatrixRowAmount;
        int sectorMatrixTileAmount = sectorMatrixColAmount * sectorMatrixRowAmount;
        int sectorColAmount = SectorColAmount;
        int sectorTileAmount = SectorColAmount * SectorRowAmount;
        NativeArray<int> sectorMarks = SectorMarks;

        //////////LOOKUP TABLE////////////
        //////////////////////////////////
        int nLocal1d;
        int eLocal1d;
        int sLocal1d;
        int wLocal1d;
        int neLocal1d;
        int seLocal1d;
        int swLocal1d;
        int nwLocal1d;
        int nSector1d;
        int eSector1d;
        int sSector1d;
        int wSector1d;
        int neSector1d;
        int seSector1d;
        int swSector1d;
        int nwSector1d;
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
            //LOCAL INDICIES
            nLocal1d = index + sectorColAmount;
            eLocal1d = index + 1;
            sLocal1d = index - sectorColAmount;
            wLocal1d = index - 1;
            neLocal1d = nLocal1d + 1;
            seLocal1d = sLocal1d + 1;
            swLocal1d = sLocal1d - 1;
            nwLocal1d = nLocal1d - 1;

            //OVERFLOWS
            bool nLocalOverflow = nLocal1d >= sectorTileAmount;
            bool eLocalOverflow = (eLocal1d % sectorColAmount) == 0;
            bool sLocalOverflow = sLocal1d < 0;
            bool wLocalOverflow = (index % sectorColAmount) == 0;

            //LOCAL OWERFLOW HANDLING
            nSector1d = math.select(pickedSector1d, pickedSector1d + sectorMatrixColAmount, nLocalOverflow);
            eSector1d = math.select(pickedSector1d, pickedSector1d + 1, eLocalOverflow);
            sSector1d = math.select(pickedSector1d, pickedSector1d - sectorMatrixColAmount, sLocalOverflow);
            wSector1d = math.select(pickedSector1d, pickedSector1d - 1, wLocalOverflow);
            neSector1d = math.select(pickedSector1d, pickedSector1d + sectorMatrixColAmount, nLocalOverflow);
            neSector1d = math.select(neSector1d, neSector1d + 1, eLocalOverflow);
            seSector1d = math.select(pickedSector1d, pickedSector1d - sectorMatrixColAmount, sLocalOverflow);
            seSector1d = math.select(seSector1d, seSector1d + 1, eLocalOverflow);
            swSector1d = math.select(pickedSector1d, pickedSector1d - sectorMatrixColAmount, sLocalOverflow);
            swSector1d = math.select(swSector1d, swSector1d - 1, wLocalOverflow);
            nwSector1d = math.select(pickedSector1d, pickedSector1d + sectorMatrixColAmount, nLocalOverflow);
            nwSector1d = math.select(nwSector1d, nwSector1d - 1, wLocalOverflow);

            nLocal1d = math.select(nLocal1d, index - (sectorColAmount * sectorColAmount - sectorColAmount), nLocalOverflow);
            eLocal1d = math.select(eLocal1d, index - sectorColAmount + 1, eLocalOverflow);
            sLocal1d = math.select(sLocal1d, index + (sectorColAmount * sectorColAmount - sectorColAmount), sLocalOverflow);
            wLocal1d = math.select(wLocal1d, index + sectorColAmount - 1, wLocalOverflow);
            neLocal1d = math.select(neLocal1d, neLocal1d - (sectorColAmount * sectorColAmount), nLocalOverflow);
            neLocal1d = math.select(neLocal1d, neLocal1d - sectorColAmount, eLocalOverflow);
            seLocal1d = math.select(seLocal1d, seLocal1d + (sectorColAmount * sectorColAmount), sLocalOverflow);
            seLocal1d = math.select(seLocal1d, seLocal1d - sectorColAmount, eLocalOverflow);
            swLocal1d = math.select(swLocal1d, swLocal1d + (sectorColAmount * sectorColAmount), sLocalOverflow);
            swLocal1d = math.select(swLocal1d, swLocal1d + sectorColAmount, wLocalOverflow);
            nwLocal1d = math.select(nwLocal1d, nwLocal1d - (sectorColAmount * sectorColAmount), nLocalOverflow);
            nwLocal1d = math.select(nwLocal1d, nwLocal1d + sectorColAmount, wLocalOverflow);

            //SECTOR OVERFLOWS
            bool nSectorOverflow = nSector1d >= sectorMatrixTileAmount;
            bool eSectorOverflow = (eSector1d % sectorMatrixColAmount) == 0 && eSector1d != pickedSector1d;
            bool sSectorOverflow = sSector1d < 0;
            bool wSectorOverflow = (pickedSector1d % sectorMatrixColAmount) == 0 && wSector1d != pickedSector1d;

            if (nSectorOverflow)
            {
                nSector1d = pickedSector1d;
                neSector1d = pickedSector1d;
                nwSector1d = pickedSector1d;
                nLocal1d = index;
                neLocal1d = index;
                nwLocal1d = index;
            }
            if (eSectorOverflow)
            {
                eSector1d = pickedSector1d;
                seSector1d = pickedSector1d;
                neSector1d = pickedSector1d;
                eLocal1d = index;
                neLocal1d = index;
                seLocal1d = index;
            }
            if (sSectorOverflow)
            {
                sSector1d = pickedSector1d;
                seSector1d = pickedSector1d;
                swSector1d = pickedSector1d;
                sLocal1d = index;
                seLocal1d = index;
                swLocal1d = index;
            }
            if (wSectorOverflow)
            {
                wSector1d = pickedSector1d;
                nwSector1d = pickedSector1d;
                swSector1d = pickedSector1d;
                wLocal1d = index;
                nwLocal1d = index;
                swLocal1d = index;
            }

            //SECTOR MARKS
            int curSectorMark = sectorMarks[pickedSector1d];
            int nSectorMark = sectorMarks[nSector1d];
            int eSectorMark = sectorMarks[eSector1d];
            int sSectorMark = sectorMarks[sSector1d];
            int wSectorMark = sectorMarks[wSector1d];
            int neSectorMark = sectorMarks[neSector1d];
            int seSectorMark = sectorMarks[seSector1d];
            int swSectorMark = sectorMarks[swSector1d];
            int nwSectorMark = sectorMarks[nwSector1d];

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

            if (nSectorMark != 0) { nIntCost = nSector[nLocal1d].Cost; }
            if (eSectorMark != 0) { eIntCost = eSector[eLocal1d].Cost; }
            if (sSectorMark != 0) { sIntCost = sSector[sLocal1d].Cost; }
            if (wSectorMark != 0) { wIntCost = wSector[wLocal1d].Cost; }
            if (neSectorMark != 0) { neIntCost = neSector[neLocal1d].Cost; }
            if (seSectorMark != 0) { seIntCost = seSector[seLocal1d].Cost; }
            if (swSectorMark != 0) { swIntCost = swSector[swLocal1d].Cost; }
            if (nwSectorMark != 0) { nwIntCost = nwSector[nwLocal1d].Cost; }
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