using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;

[BurstCompile]
public struct FlowFieldJob : IJobParallelFor
{
    public int SectorMatrixColAmount;
    public int SectorMatrixRowAmount;
    public int SectorColAmount;
    public int SectorRowAmount;
    public int SectorTileAmount;
    [ReadOnly] public NativeArray<int> SectorToPicked;
    [ReadOnly] public NativeArray<int> PickedToSector;
    [ReadOnly] public NativeArray<IntegrationTile> IntegrationField;
    [WriteOnly] public NativeArray<FlowData> FlowField;

    public void Execute(int index)
    {
        if(index == 0) { return; }
        if (IntegrationField[index].Mark == IntegrationMark.LOSPass) { FlowField[index] = FlowData.LOS; return; }

        //DATA
        NativeArray<int> sectorToPicked = SectorToPicked;
        NativeArray<IntegrationTile> integrationField = IntegrationField;
        int localIndex = (index - 1) % SectorTileAmount;
        int pickedSector1d = PickedToSector[(index - 1) / SectorTileAmount];
        int sectorMatrixColAmount = SectorMatrixColAmount;
        int sectorMatrixRowAmount = SectorMatrixRowAmount;
        int sectorMatrixTileAmount = sectorMatrixColAmount * sectorMatrixRowAmount;
        int sectorColAmount = SectorColAmount;
        int sectorTileAmount = SectorTileAmount;

        //////////LOOKUP TABLE////////////
        //////////////////////////////////
        float nIntCost;
        float eIntCost;
        float sIntCost;
        float wIntCost;
        float neIntCost;
        float seIntCost;
        float swIntCost;
        float nwIntCost;
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
        FlowField[index] = minFlow;

        void SetLookupTable()
        {
            //LOCAL INDICIES
            int nLocal1d = localIndex + sectorColAmount;
            int eLocal1d = localIndex + 1;
            int sLocal1d = localIndex - sectorColAmount;
            int wLocal1d = localIndex - 1;
            int neLocal1d = nLocal1d + 1;
            int seLocal1d = sLocal1d + 1;
            int swLocal1d = sLocal1d - 1;
            int nwLocal1d = nLocal1d - 1;

            //OVERFLOWS
            bool nLocalOverflow = nLocal1d >= sectorTileAmount;
            bool eLocalOverflow = (eLocal1d % sectorColAmount) == 0;
            bool sLocalOverflow = sLocal1d < 0;
            bool wLocalOverflow = (localIndex % sectorColAmount) == 0;

            //LOCAL OWERFLOW HANDLING
            int nSector1d = math.select(pickedSector1d, pickedSector1d + sectorMatrixColAmount, nLocalOverflow);
            int eSector1d = math.select(pickedSector1d, pickedSector1d + 1, eLocalOverflow);
            int sSector1d = math.select(pickedSector1d, pickedSector1d - sectorMatrixColAmount, sLocalOverflow);
            int wSector1d = math.select(pickedSector1d, pickedSector1d - 1, wLocalOverflow);
            int neSector1d = math.select(pickedSector1d, pickedSector1d + sectorMatrixColAmount, nLocalOverflow);
            neSector1d = math.select(neSector1d, neSector1d + 1, eLocalOverflow);
            int seSector1d = math.select(pickedSector1d, pickedSector1d - sectorMatrixColAmount, sLocalOverflow);
            seSector1d = math.select(seSector1d, seSector1d + 1, eLocalOverflow);
            int swSector1d = math.select(pickedSector1d, pickedSector1d - sectorMatrixColAmount, sLocalOverflow);
            swSector1d = math.select(swSector1d, swSector1d - 1, wLocalOverflow);
            int nwSector1d = math.select(pickedSector1d, pickedSector1d + sectorMatrixColAmount, nLocalOverflow);
            nwSector1d = math.select(nwSector1d, nwSector1d - 1, wLocalOverflow);

            nLocal1d = math.select(nLocal1d, localIndex - (sectorColAmount * sectorColAmount - sectorColAmount), nLocalOverflow);
            eLocal1d = math.select(eLocal1d, localIndex - sectorColAmount + 1, eLocalOverflow);
            sLocal1d = math.select(sLocal1d, localIndex + (sectorColAmount * sectorColAmount - sectorColAmount), sLocalOverflow);
            wLocal1d = math.select(wLocal1d, localIndex + sectorColAmount - 1, wLocalOverflow);
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
                nLocal1d = localIndex;
                neLocal1d = localIndex;
                nwLocal1d = localIndex;
            }
            if (eSectorOverflow)
            {
                eSector1d = pickedSector1d;
                seSector1d = pickedSector1d;
                neSector1d = pickedSector1d;
                eLocal1d = localIndex;
                neLocal1d = localIndex;
                seLocal1d = localIndex;
            }
            if (sSectorOverflow)
            {
                sSector1d = pickedSector1d;
                seSector1d = pickedSector1d;
                swSector1d = pickedSector1d;
                sLocal1d = localIndex;
                seLocal1d = localIndex;
                swLocal1d = localIndex;
            }
            if (wSectorOverflow)
            {
                wSector1d = pickedSector1d;
                nwSector1d = pickedSector1d;
                swSector1d = pickedSector1d;
                wLocal1d = localIndex;
                nwLocal1d = localIndex;
                swLocal1d = localIndex;
            }

            //SECTOR MARKS
            int nSectorMark = sectorToPicked[nSector1d];
            int eSectorMark = sectorToPicked[eSector1d];
            int sSectorMark = sectorToPicked[sSector1d];
            int wSectorMark = sectorToPicked[wSector1d];
            int neSectorMark = sectorToPicked[neSector1d];
            int seSectorMark = sectorToPicked[seSector1d];
            int swSectorMark = sectorToPicked[swSector1d];
            int nwSectorMark = sectorToPicked[nwSector1d];

            //INTEGRATED COSTS
            nIntCost = float.MaxValue;
            eIntCost = float.MaxValue;
            sIntCost = float.MaxValue;
            wIntCost = float.MaxValue;
            neIntCost = float.MaxValue;
            seIntCost = float.MaxValue;
            swIntCost = float.MaxValue;
            nwIntCost = float.MaxValue;
            if (nSectorMark != 0) { nIntCost = integrationField[nSectorMark + nLocal1d].Cost; }
            if (eSectorMark != 0) { eIntCost = integrationField[eSectorMark + eLocal1d].Cost; }
            if (sSectorMark != 0) { sIntCost = integrationField[sSectorMark + sLocal1d].Cost; }
            if (wSectorMark != 0) { wIntCost = integrationField[wSectorMark + wLocal1d].Cost; }
            if (neSectorMark != 0) { neIntCost = integrationField[neSectorMark + neLocal1d].Cost; }
            if (seSectorMark != 0) { seIntCost = integrationField[seSectorMark + seLocal1d].Cost; }
            if (swSectorMark != 0) { swIntCost = integrationField[swSectorMark + swLocal1d].Cost; }
            if (nwSectorMark != 0) { nwIntCost = integrationField[nwSectorMark + nwLocal1d].Cost; }
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