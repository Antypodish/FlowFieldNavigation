using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using System;

[BurstCompile]
public struct FlowFieldJob : IJobParallelFor
{
    public int SectorMatrixColAmount;
    public int SectorMatrixRowAmount;
    public int SectorMatrixTileAmount;
    public int SectorColAmount;
    public int SectorRowAmount;
    public int SectorTileAmount;
    public int FieldColAmount;
    public int FieldTileAmount;
    public int SectorStartIndex;
    [ReadOnly] public UnsafeList<int> SectorToPicked;
    [ReadOnly] public NativeArray<int> PickedToSector;
    [ReadOnly] public NativeArray<IntegrationTile> IntegrationField;
    [WriteOnly] public UnsafeList<FlowData> FlowFieldCalculationBuffer;

    public void Execute(int index)
    {
        //DATA
        int flowFieldStridedIndex = SectorStartIndex + index;
        int startLocalIndex = (flowFieldStridedIndex - 1) % SectorTileAmount;
        int startPickedSector1d = PickedToSector[(flowFieldStridedIndex - 1) / SectorTileAmount];

        int curFlowFieldIndex = flowFieldStridedIndex;
        int curLocalIndex = startLocalIndex;
        int curPickedSector1d = startPickedSector1d;
        float curIntCost = IntegrationField[curFlowFieldIndex].Cost;

        int verDif = 0;
        int horDif = 0;
        while(curIntCost != 0)
        {
            NewIndexData newIndex = GetNextIndex(curLocalIndex, curPickedSector1d, startPickedSector1d, curIntCost, horDif, verDif);
            if (newIndex.Index == 0) { break; }
            int newFlowFieldIndex = newIndex.Index;
            verDif = newIndex.NewVerDif;
            horDif = newIndex.NewHorDif;
            curFlowFieldIndex = newFlowFieldIndex;
            curLocalIndex = (newFlowFieldIndex - 1) % SectorTileAmount;
            curPickedSector1d = PickedToSector[(curFlowFieldIndex - 1) / SectorTileAmount];
            curIntCost = IntegrationField[curFlowFieldIndex].Cost;
        }
        int endLocalIndex = curLocalIndex;
        int endSectorIndex = curPickedSector1d;

        int2 startLocal2d = FlowFieldUtilities.To2D(startLocalIndex, SectorColAmount);
        int2 endLocal2d = FlowFieldUtilities.To2D(endLocalIndex, SectorColAmount);

        int2 startSector2d = FlowFieldUtilities.To2D(startPickedSector1d, SectorMatrixColAmount);
        int2 endSector2d = FlowFieldUtilities.To2D(endSectorIndex, SectorMatrixColAmount);


        int startGeneral1d = FlowFieldUtilities.GetGeneral1d(startLocal2d, startSector2d, SectorColAmount, FieldColAmount);
        int endGeneral1d = FlowFieldUtilities.GetGeneral1d(endLocal2d, endSector2d, SectorColAmount, FieldColAmount);


        FlowData flow = new FlowData();
        flow.SetFlow(startGeneral1d, endGeneral1d, FieldColAmount);
        if(curIntCost == 0) { flow.SetLOS(); }
        FlowFieldCalculationBuffer[index] = flow;
    }

    NewIndexData GetNextIndex(int localIndex, int pickedSector1d, int startingSector1d, float curIntCost, int horizontalDif, int verticalDif)
    {
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
        bool nSectorOverflow = nSector1d >= SectorMatrixTileAmount;
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


        int maxNSector = startingSector1d + SectorMatrixColAmount;
        int maxESector = startingSector1d + 1;
        int maxSSector = startingSector1d - SectorMatrixColAmount;
        int maxWSector = startingSector1d - 1;
        bool nAdjacent = nSector1d == startingSector1d || nSector1d == maxNSector || nSector1d == maxESector || nSector1d == maxWSector || nSector1d == maxSSector;
        bool eAdjacent = eSector1d == startingSector1d || eSector1d == maxNSector || eSector1d == maxESector || eSector1d == maxWSector || eSector1d == maxSSector;
        bool sAdjacent = sSector1d == startingSector1d || sSector1d == maxNSector || sSector1d == maxESector || sSector1d == maxWSector || sSector1d == maxSSector;
        bool wAdjacent = wSector1d == startingSector1d || wSector1d == maxNSector || wSector1d == maxESector || wSector1d == maxWSector || wSector1d == maxSSector;

        int upperDif = verticalDif + 1;
        int lowerDif = verticalDif - 1;
        int rightDif = horizontalDif + 1;
        int leftDif = horizontalDif - 1;

        bool upAvailable = upperDif <= 7;
        bool lowAvailable = lowerDif >= -7;
        bool rightAvailable = rightDif <= 7;
        bool leftAvailable = leftDif >= -7;

        //INTEGRATED COSTS
        float nIntCost = float.MaxValue;
        float eIntCost = float.MaxValue;
        float sIntCost = float.MaxValue;
        float wIntCost = float.MaxValue;
        float neIntCost = float.MaxValue;
        float seIntCost = float.MaxValue;
        float swIntCost = float.MaxValue;
        float nwIntCost = float.MaxValue;
        if (nSectorMark != 0 && nAdjacent && upAvailable) { nIntCost = IntegrationField[nSectorMark + nLocal1d].Cost; }
        if (eSectorMark != 0 && eAdjacent && rightAvailable) { eIntCost = IntegrationField[eSectorMark + eLocal1d].Cost; }
        if (sSectorMark != 0 && sAdjacent && lowAvailable) { sIntCost = IntegrationField[sSectorMark + sLocal1d].Cost; }
        if (wSectorMark != 0 && wAdjacent && leftAvailable) { wIntCost = IntegrationField[wSectorMark + wLocal1d].Cost; }
        if (neSectorMark != 0 && nAdjacent && eAdjacent && upAvailable && rightAvailable) { neIntCost = IntegrationField[neSectorMark + neLocal1d].Cost; }
        if (seSectorMark != 0 && sAdjacent && eAdjacent && lowAvailable && rightAvailable) { seIntCost = IntegrationField[seSectorMark + seLocal1d].Cost; }
        if (swSectorMark != 0 && sAdjacent && wAdjacent &&  lowAvailable && leftAvailable) { swIntCost = IntegrationField[swSectorMark + swLocal1d].Cost; }
        if (nwSectorMark != 0 && nAdjacent && wAdjacent && upAvailable && leftAvailable) { nwIntCost = IntegrationField[nwSectorMark + nwLocal1d].Cost; }
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
        float minCost = curIntCost;
        int minIndex = 0;
        int newVerDif = verticalDif;
        int newHorDif = horizontalDif;
        if (nIntCost < minCost) { minCost = nIntCost; minIndex = nLocal1d + nSectorMark; newVerDif = verticalDif + 1; }
        if (eIntCost < minCost) { minCost = eIntCost; minIndex = eLocal1d + eSectorMark; newHorDif = horizontalDif + 1; }
        if (sIntCost < minCost) { minCost = sIntCost; minIndex = sLocal1d + sSectorMark; newVerDif = verticalDif - 1; }
        if (wIntCost < minCost) { minCost = wIntCost; minIndex = wLocal1d + wSectorMark; newHorDif = horizontalDif - 1; }
        if (neIntCost < minCost) { minCost = neIntCost; minIndex = neLocal1d + neSectorMark; newVerDif = verticalDif + 1; newHorDif = horizontalDif + 1; }
        if (seIntCost < minCost) { minCost = seIntCost; minIndex = seLocal1d + seSectorMark; newVerDif = verticalDif - 1; newHorDif = horizontalDif + 1; }
        if (swIntCost < minCost) { minCost = swIntCost; minIndex = swLocal1d + swSectorMark; newVerDif = verticalDif - 1; newHorDif = horizontalDif - 1; } 
        if (nwIntCost < minCost) { minCost = nwIntCost; minIndex = nwLocal1d + nwSectorMark; newVerDif = verticalDif + 1; newHorDif = horizontalDif - 1; }

        return new NewIndexData()
        {
            Index = minIndex,
            NewHorDif = newHorDif,
            NewVerDif = newVerDif,
        };
    }
    private struct NewIndexData
    {
        public int Index;
        public int NewVerDif;
        public int NewHorDif;
    }
}
[BurstCompile]
public struct FlowData
{
    byte _flow;

    public float2 GetFlow(float tileSize)
    {
        int verticalMag = (_flow >> 4) & 0b0000_0111;
        int horizontalMag = _flow & 0b0000_0111;

        bool isVerticalNegative = (_flow & 0b1000_0000) == 0b1000_0000;
        bool isHorizontalNegative = (_flow & 0b0000_1000) == 0b0000_1000;

        verticalMag = math.select(verticalMag, -(verticalMag + 1), isVerticalNegative);
        horizontalMag = math.select(horizontalMag, -(horizontalMag+ 1), isHorizontalNegative);

        return math.normalizesafe(new float2(horizontalMag * tileSize, verticalMag * tileSize));
    }
    public void SetFlow(int curGeneralIndex, int targetGeneralIndex, int fieldColAmount)
    {
        int verticalDif = (targetGeneralIndex / fieldColAmount - curGeneralIndex / fieldColAmount);//-1
        int horizontalDif = targetGeneralIndex - (curGeneralIndex + verticalDif * fieldColAmount);//+1

        if(verticalDif > 7 || verticalDif < -7 || horizontalDif > 7 || horizontalDif < -7) { return; }
        bool isVerticalNegative = verticalDif < 0;
        bool isHorizontalNegative = horizontalDif < 0;

        byte verticalBits = (byte) math.select(verticalDif << 4, ((math.abs(verticalDif) - 1) << 4) | 0b1000_0000, isVerticalNegative);
        byte horizontalBits = (byte) math.select(horizontalDif, (math.abs(horizontalDif) - 1) | 0b0000_1000, isHorizontalNegative);
        _flow = (byte)(0 | verticalBits | horizontalBits);
    }
    public void SetLOS()
    {
        _flow = 0b1111_1111;
    }
    public bool IsLOS()
    {
        return _flow == 0b1111_1111;
    }
    public bool IsValid()
    {
        return _flow != 0b0000_00000;
    }
}