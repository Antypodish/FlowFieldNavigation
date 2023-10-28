using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Mathematics;

public struct NewFlowFieldJob : IJobParallelFor
{

    public int SectorMatrixColAmount;
    public int SectorMatrixRowAmount;
    public int SectorColAmount;
    public int SectorRowAmount;
    public int SectorTileAmount;
    [ReadOnly] public UnsafeList<int> SectorToPicked;
    [ReadOnly] public NativeArray<int> PickedToSector;
    [ReadOnly] public NativeArray<IntegrationTile> IntegrationField;
    [WriteOnly] public UnsafeList<FlowData> FlowField;

    public void Execute(int index)
    {
        if (index == 0) { return; }
        if (IntegrationField[index].Mark == IntegrationMark.LOSPass && IntegrationField[index].Cost == 0) { FlowField[index] = FlowData.LOS; return; }

        //DATA
        int localIndex = (index - 1) % SectorTileAmount;
        int pickedSector1d = PickedToSector[(index - 1) / SectorTileAmount];
        int sectorMatrixTileAmount = SectorMatrixColAmount * SectorMatrixRowAmount;

        //LOCAL INDICIES
        int nLocal1d = localIndex + SectorColAmount;
        int eLocal1d = localIndex + 1;
        int sLocal1d = localIndex - SectorColAmount;
        int wLocal1d = localIndex - 1;
        int neLocal1d = nLocal1d + 1;
        int seLocal1d = sLocal1d + 1;
        int swLocal1d = sLocal1d - 1;
        int nwLocal1d = nLocal1d - 1;

        //OVERFLOWS
        bool nLocalOverflow = nLocal1d >= SectorTileAmount;
        bool eLocalOverflow = (eLocal1d % SectorColAmount) == 0;
        bool sLocalOverflow = sLocal1d < 0;
        bool wLocalOverflow = (localIndex % SectorColAmount) == 0;

        //LOCAL OWERFLOW HANDLING
        int nSector1d = math.select(pickedSector1d, pickedSector1d + SectorMatrixColAmount, nLocalOverflow);
        int eSector1d = math.select(pickedSector1d, pickedSector1d + 1, eLocalOverflow);
        int sSector1d = math.select(pickedSector1d, pickedSector1d - SectorMatrixColAmount, sLocalOverflow);
        int wSector1d = math.select(pickedSector1d, pickedSector1d - 1, wLocalOverflow);
        int neSector1d = math.select(pickedSector1d, pickedSector1d + SectorMatrixColAmount, nLocalOverflow);
        neSector1d = math.select(neSector1d, neSector1d + 1, eLocalOverflow);
        int seSector1d = math.select(pickedSector1d, pickedSector1d - SectorMatrixColAmount, sLocalOverflow);
        seSector1d = math.select(seSector1d, seSector1d + 1, eLocalOverflow);
        int swSector1d = math.select(pickedSector1d, pickedSector1d - SectorMatrixColAmount, sLocalOverflow);
        swSector1d = math.select(swSector1d, swSector1d - 1, wLocalOverflow);
        int nwSector1d = math.select(pickedSector1d, pickedSector1d + SectorMatrixColAmount, nLocalOverflow);
        nwSector1d = math.select(nwSector1d, nwSector1d - 1, wLocalOverflow);

        nLocal1d = math.select(nLocal1d, localIndex - (SectorColAmount * SectorColAmount - SectorColAmount), nLocalOverflow);
        eLocal1d = math.select(eLocal1d, localIndex - SectorColAmount + 1, eLocalOverflow);
        sLocal1d = math.select(sLocal1d, localIndex + (SectorColAmount * SectorColAmount - SectorColAmount), sLocalOverflow);
        wLocal1d = math.select(wLocal1d, localIndex + SectorColAmount - 1, wLocalOverflow);
        neLocal1d = math.select(neLocal1d, neLocal1d - (SectorColAmount * SectorColAmount), nLocalOverflow);
        neLocal1d = math.select(neLocal1d, neLocal1d - SectorColAmount, eLocalOverflow);
        seLocal1d = math.select(seLocal1d, seLocal1d + (SectorColAmount * SectorColAmount), sLocalOverflow);
        seLocal1d = math.select(seLocal1d, seLocal1d - SectorColAmount, eLocalOverflow);
        swLocal1d = math.select(swLocal1d, swLocal1d + (SectorColAmount * SectorColAmount), sLocalOverflow);
        swLocal1d = math.select(swLocal1d, swLocal1d + SectorColAmount, wLocalOverflow);
        nwLocal1d = math.select(nwLocal1d, nwLocal1d - (SectorColAmount * SectorColAmount), nLocalOverflow);
        nwLocal1d = math.select(nwLocal1d, nwLocal1d + SectorColAmount, wLocalOverflow);

        //SECTOR OVERFLOWS
        bool nSectorOverflow = nSector1d >= sectorMatrixTileAmount;
        bool eSectorOverflow = (eSector1d % SectorMatrixColAmount) == 0 && eSector1d != pickedSector1d;
        bool sSectorOverflow = sSector1d < 0;
        bool wSectorOverflow = (pickedSector1d % SectorMatrixColAmount) == 0 && wSector1d != pickedSector1d;

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
        int nSectorMark = SectorToPicked[nSector1d];
        int eSectorMark = SectorToPicked[eSector1d];
        int sSectorMark = SectorToPicked[sSector1d];
        int wSectorMark = SectorToPicked[wSector1d];
        int neSectorMark = SectorToPicked[neSector1d];
        int seSectorMark = SectorToPicked[seSector1d];
        int swSectorMark = SectorToPicked[swSector1d];
        int nwSectorMark = SectorToPicked[nwSector1d];

        //INTEGRATED COSTS
        float curIntCost = IntegrationField[index].Cost;
        float nIntCost = float.MaxValue;
        float eIntCost = float.MaxValue;
        float sIntCost = float.MaxValue;
        float wIntCost = float.MaxValue;
        float neIntCost = float.MaxValue;
        float seIntCost = float.MaxValue;
        float swIntCost = float.MaxValue;
        float nwIntCost = float.MaxValue;
        if (nSectorMark != 0) { nIntCost = IntegrationField[nSectorMark + nLocal1d].Cost; }
        if (eSectorMark != 0) { eIntCost = IntegrationField[eSectorMark + eLocal1d].Cost; }
        if (sSectorMark != 0) { sIntCost = IntegrationField[sSectorMark + sLocal1d].Cost; }
        if (wSectorMark != 0) { wIntCost = IntegrationField[wSectorMark + wLocal1d].Cost; }
        if (neSectorMark != 0) { neIntCost = IntegrationField[neSectorMark + neLocal1d].Cost; }
        if (seSectorMark != 0) { seIntCost = IntegrationField[seSectorMark + seLocal1d].Cost; }
        if (swSectorMark != 0) { swIntCost = IntegrationField[swSectorMark + swLocal1d].Cost; }
        if (nwSectorMark != 0) { nwIntCost = IntegrationField[nwSectorMark + nwLocal1d].Cost; }
        if (curIntCost != float.MaxValue)
        {
            if (nIntCost == float.MaxValue)
            {
                neIntCost = float.MaxValue;
                nwIntCost = float.MaxValue;
            }
            if (eIntCost == float.MaxValue)
            {
                neIntCost = float.MaxValue;
                seIntCost = float.MaxValue;
            }
            if (sIntCost == float.MaxValue)
            {
                seIntCost = float.MaxValue;
                swIntCost = float.MaxValue;
            }
            if (wIntCost == float.MaxValue)
            {
                nwIntCost = float.MaxValue;
                swIntCost = float.MaxValue;
            }
        }
        float minCost = float.MaxValue;
        FlowData minFlow = FlowData.None;
        if (nIntCost < minCost) { minCost = nIntCost; minFlow = FlowData.N; }
        if (eIntCost < minCost) { minCost = eIntCost; minFlow = FlowData.E; }
        if (sIntCost < minCost) { minCost = sIntCost; minFlow = FlowData.S; }
        if (wIntCost < minCost) { minCost = wIntCost; minFlow = FlowData.W; }
        if (neIntCost < minCost) { minCost = neIntCost; minFlow = FlowData.NE; }
        if (seIntCost < minCost) { minCost = seIntCost; minFlow = FlowData.SE; }
        if (swIntCost < minCost) { minCost = swIntCost; minFlow = FlowData.SW; }
        if (nwIntCost < minCost) { minCost = nwIntCost; minFlow = FlowData.NW; }
        FlowField[index] = minFlow;
    }
}