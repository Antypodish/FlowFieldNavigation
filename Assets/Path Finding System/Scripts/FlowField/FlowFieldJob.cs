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
    public float TileSize;
    [ReadOnly] public UnsafeList<int> SectorToPicked;
    [ReadOnly] public NativeArray<int> PickedToSector;
    [ReadOnly] public NativeArray<IntegrationTile> IntegrationField;
    [WriteOnly] public UnsafeList<FlowData> FlowFieldCalculationBuffer;
    [ReadOnly] public NativeArray<UnsafeList<byte>> Costs;

    public void Execute(int index)
    {
        //START DATA
        float sectorSize = SectorColAmount * TileSize;
        int flowFieldStridedIndex = SectorStartIndex + index;
        int startLocalIndex = (flowFieldStridedIndex - 1) % SectorTileAmount;
        int startPickedSector1d = PickedToSector[(flowFieldStridedIndex - 1) / SectorTileAmount];
        int2 startLocal2d = FlowFieldUtilities.To2D(startLocalIndex, SectorColAmount);
        int2 startSector2d = FlowFieldUtilities.To2D(startPickedSector1d, SectorMatrixColAmount);
        int2 startGeneral2d = FlowFieldUtilities.GetGeneral2d(startLocal2d, startSector2d, SectorColAmount, FieldColAmount);

        //LOOP DATA
        int curFlowFieldIndex = flowFieldStridedIndex;
        int curLocalIndex = startLocalIndex;
        int curPickedSector1d = startPickedSector1d;
        float curIntCost = IntegrationField[curFlowFieldIndex].Cost;
        int verDif = 0;
        int horDif = 0;
        int bestLocal1d = startLocalIndex;
        int bestSector1d = startPickedSector1d;
        float bestIntCost = curIntCost;
        float2 startPos = FlowFieldUtilities.LocalIndexToPos(startLocalIndex, startPickedSector1d, SectorMatrixColAmount, SectorColAmount, TileSize, sectorSize);
        float2 lastCornerPos = 0;
        CornerBlockDirection lastCornerBlockDirection = CornerBlockDirection.None;
        //LOOP
        while (curIntCost != 0)
        {
            NewIndexData newIndex = GetNextIndex(curLocalIndex, curPickedSector1d, startPickedSector1d, curIntCost, horDif, verDif, startGeneral2d, startPos);
            if (newIndex.Index == 0) { break; }

            int newFlowFieldIndex = newIndex.Index;
            int newVerDif = newIndex.NewVerDif;
            int newHorDif = newIndex.NewHorDif;
            int newLocalIndex = (newFlowFieldIndex - 1) % SectorTileAmount;
            int newPickedSector1d = PickedToSector[(newFlowFieldIndex - 1) / SectorTileAmount];
            float newIntCost = IntegrationField[newFlowFieldIndex].Cost;

            if (newIndex.CornerBlockDirection != CornerBlockDirection.None)
            {
                if(lastCornerBlockDirection == CornerBlockDirection.None)
                {
                    float2 curIndexPos = FlowFieldUtilities.LocalIndexToPos(curLocalIndex, curPickedSector1d, SectorMatrixColAmount, SectorColAmount, TileSize, sectorSize);
                    lastCornerPos = curIndexPos;
                    lastCornerBlockDirection = newIndex.CornerBlockDirection;
                }
                else
                {
                    float2 curIndexPos = FlowFieldUtilities.LocalIndexToPos(curLocalIndex, curPickedSector1d, SectorMatrixColAmount, SectorColAmount, TileSize, sectorSize);
                    if (!IsBlocked(startPos, lastCornerPos, lastCornerBlockDirection, curIndexPos))
                    {
                        lastCornerPos = curIndexPos;
                        lastCornerBlockDirection = newIndex.CornerBlockDirection;
                    }
                }
                
            }

            if (lastCornerBlockDirection != CornerBlockDirection.None)
            {
                float2 newIndexPos = FlowFieldUtilities.LocalIndexToPos(newLocalIndex, newPickedSector1d, SectorMatrixColAmount, SectorColAmount, TileSize, sectorSize);
                if (!IsBlocked(startPos, lastCornerPos, lastCornerBlockDirection, newIndexPos))
                {
                    bestLocal1d = newLocalIndex;
                    bestSector1d = newPickedSector1d;
                    bestIntCost = newIntCost;
                }
                verDif = newVerDif;
                horDif = newHorDif;
                curFlowFieldIndex = newFlowFieldIndex;
                curLocalIndex = newLocalIndex;
                curPickedSector1d = newPickedSector1d;
                curIntCost = newIntCost;
            }
            else
            {
                
                verDif = newVerDif;
                horDif = newHorDif;
                curFlowFieldIndex = newFlowFieldIndex;
                curLocalIndex = newLocalIndex;
                curPickedSector1d = newPickedSector1d;
                curIntCost = newIntCost;
                bestLocal1d = curLocalIndex;
                bestSector1d = curPickedSector1d;
                bestIntCost = curIntCost;
            }
        }

        int2 endLocal2d = FlowFieldUtilities.To2D(bestLocal1d, SectorColAmount);
        int2 endSector2d = FlowFieldUtilities.To2D(bestSector1d, SectorMatrixColAmount);
        int startGeneral1d = FlowFieldUtilities.GetGeneral1d(startLocal2d, startSector2d, SectorColAmount, FieldColAmount);
        int endGeneral1d = FlowFieldUtilities.GetGeneral1d(endLocal2d, endSector2d, SectorColAmount, FieldColAmount);
        FlowData flow = new FlowData();
        flow.SetFlow(startGeneral1d, endGeneral1d, FieldColAmount);
        FlowFieldCalculationBuffer[index] = flow;
    }

    NewIndexData GetNextIndex(int localIndex, int pickedSector1d, int originalSector1d, float curIntCost, int horizontalDif, int verticalDif, int2 startGeneral2d, float2 startPos)
    {
        int2 curLocal2d = FlowFieldUtilities.To2D(localIndex, SectorColAmount);
        int2 curSector2d = FlowFieldUtilities.To2D(pickedSector1d, SectorMatrixColAmount);
        int2 curGeneral2d = FlowFieldUtilities.GetGeneral2d(curLocal2d, curSector2d, SectorColAmount, FieldColAmount);

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

        //SIGHT CORNER OUTPUT
        CornerBlockDirection cornerDir = CornerBlockDirection.None;

        //SIGHT CORNER TEST
        bool nBlocked = Costs[nSector1d][nLocal1d] == byte.MaxValue;
        bool eBlocked = Costs[eSector1d][eLocal1d] == byte.MaxValue;
        bool sBlocked = Costs[sSector1d][sLocal1d] == byte.MaxValue;
        bool wBlocked = Costs[wSector1d][wLocal1d] == byte.MaxValue;
        bool neBlocked = Costs[neSector1d][neLocal1d] == byte.MaxValue;
        bool seBlocked = Costs[seSector1d][seLocal1d] == byte.MaxValue;
        bool swBlocked = Costs[swSector1d][swLocal1d] == byte.MaxValue;
        bool nwBlocked = Costs[nwSector1d][nwLocal1d] == byte.MaxValue;

        bool neCorner = neBlocked && !nBlocked && !eBlocked;
        bool seCorner = seBlocked && !sBlocked && !eBlocked;
        bool swCorner = swBlocked && !sBlocked && !wBlocked;
        bool nwCorner = nwBlocked && !nBlocked && !wBlocked;

        bool moved = horizontalDif != 0 || verticalDif != 0;

        float2 tileOffset = new float2(TileSize / 2, TileSize / 2);
        if (neCorner)
        {
            int2 neSector2d = FlowFieldUtilities.To2D(neSector1d, SectorMatrixColAmount);
            int2 neLocal2d = FlowFieldUtilities.To2D(neLocal1d, SectorColAmount);
            int2 neGeneral2d = FlowFieldUtilities.GetGeneral2d(neLocal2d, neSector2d, SectorColAmount, FieldColAmount);
            if(IsSightCorner(startGeneral2d, curGeneral2d, neGeneral2d))
            {
                float2 nePos = (float2)neGeneral2d + tileOffset;
                float2 curPos = (float2)curGeneral2d + tileOffset;
                float2 neDir = nePos - startPos;
                float2 curDir = curPos - startPos;
                bool isRight = curDir.x * neDir.y + curDir.y * -neDir.x < 0;
                cornerDir |= isRight ? CornerBlockDirection.Right : CornerBlockDirection.Left;
            }
        }
        if (seCorner)
        {
            int2 seSector2d = FlowFieldUtilities.To2D(seSector1d, SectorMatrixColAmount);
            int2 seLocal2d = FlowFieldUtilities.To2D(seLocal1d, SectorColAmount);
            int2 seGeneral2d = FlowFieldUtilities.GetGeneral2d(seLocal2d, seSector2d, SectorColAmount, FieldColAmount);
            if (IsSightCorner(startGeneral2d, curGeneral2d, seGeneral2d))
            {
                float2 sePos = (float2)seGeneral2d + tileOffset;
                float2 curPos = (float2)curGeneral2d + tileOffset;
                float2 seDir = sePos - startPos;
                float2 curDir = curPos - startPos;
                bool isRight = curDir.x * seDir.y + curDir.y * -seDir.x < 0;
                cornerDir |= isRight ? CornerBlockDirection.Right : CornerBlockDirection.Left;
            }
        }
        if (swCorner)
        {
            int2 swSector2d = FlowFieldUtilities.To2D(swSector1d, SectorMatrixColAmount);
            int2 swLocal2d = FlowFieldUtilities.To2D(swLocal1d, SectorColAmount);
            int2 swGeneral2d = FlowFieldUtilities.GetGeneral2d(swLocal2d, swSector2d, SectorColAmount, FieldColAmount);
            if (IsSightCorner(startGeneral2d, curGeneral2d, swGeneral2d))
            {
                float2 swPos = (float2)swGeneral2d + tileOffset;
                float2 curPos = (float2)curGeneral2d + tileOffset;
                float2 swDir = swPos - startPos;
                float2 curDir = curPos - startPos;
                bool isRight = curDir.x * swDir.y + curDir.y * -swDir.x < 0;
                cornerDir |= isRight ? CornerBlockDirection.Right : CornerBlockDirection.Left;
            }
        }
        if (nwCorner)
        {
            int2 nwSector2d = FlowFieldUtilities.To2D(nwSector1d, SectorMatrixColAmount);
            int2 nwLocal2d = FlowFieldUtilities.To2D(nwLocal1d, SectorColAmount);
            int2 nwGeneral2d = FlowFieldUtilities.GetGeneral2d(nwLocal2d, nwSector2d, SectorColAmount, FieldColAmount);
            if (IsSightCorner(startGeneral2d, curGeneral2d, nwGeneral2d))
            {
                float2 nwPos = (float2)nwGeneral2d + tileOffset;
                float2 curPos = (float2)curGeneral2d + tileOffset;
                float2 nwDir = nwPos - startPos;
                float2 curDir = curPos - startPos;
                bool isRight = curDir.x * nwDir.y + curDir.y * -nwDir.x < 0;
                cornerDir |= isRight ? CornerBlockDirection.Right : CornerBlockDirection.Left;
            }
        }
        cornerDir = moved ? cornerDir : CornerBlockDirection.None;

        //SECTOR MARKS
        int nSectorMark = SectorToPicked[nSector1d];
        int eSectorMark = SectorToPicked[eSector1d];
        int sSectorMark = SectorToPicked[sSector1d];
        int wSectorMark = SectorToPicked[wSector1d];
        int neSectorMark = SectorToPicked[neSector1d];
        int seSectorMark = SectorToPicked[seSector1d];
        int swSectorMark = SectorToPicked[swSector1d];
        int nwSectorMark = SectorToPicked[nwSector1d];


        int maxNSector = originalSector1d + SectorMatrixColAmount;
        int maxESector = originalSector1d + 1;
        int maxSSector = originalSector1d - SectorMatrixColAmount;
        int maxWSector = originalSector1d - 1;
        bool nAdjacent = nSector1d == originalSector1d || nSector1d == maxNSector || nSector1d == maxESector || nSector1d == maxWSector || nSector1d == maxSSector;
        bool eAdjacent = eSector1d == originalSector1d || eSector1d == maxNSector || eSector1d == maxESector || eSector1d == maxWSector || eSector1d == maxSSector;
        bool sAdjacent = sSector1d == originalSector1d || sSector1d == maxNSector || sSector1d == maxESector || sSector1d == maxWSector || sSector1d == maxSSector;
        bool wAdjacent = wSector1d == originalSector1d || wSector1d == maxNSector || wSector1d == maxESector || wSector1d == maxWSector || wSector1d == maxSSector;

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
        if (swSectorMark != 0 && sAdjacent && wAdjacent && lowAvailable && leftAvailable) { swIntCost = IntegrationField[swSectorMark + swLocal1d].Cost; }
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
            CornerBlockDirection = cornerDir,
        };
    }
    bool IsBlocked(float2 startPos, float2 cornerPos, CornerBlockDirection blockDirection, float2 examinedPos)
    {
        float2 cornerDir = cornerPos - startPos;
        float2 examinedDir = examinedPos - startPos;
        float aproximityTest = math.dot(cornerDir, examinedDir);
        float rightTestDot = cornerDir.x * examinedDir.y + cornerDir.y * -examinedDir.x;
        bool isRightBlocked = (blockDirection & CornerBlockDirection.Right) == CornerBlockDirection.Right;
        bool isLeftBlocked = (blockDirection & CornerBlockDirection.Left) == CornerBlockDirection.Left;
        bool isBlocked = (isRightBlocked && rightTestDot < 0) || (isLeftBlocked && rightTestDot >= 0);
        return isBlocked || aproximityTest <= 0;
    }
    bool IsSightCorner(int2 startGeneral2d, int2 examinedGeneral2d, int2 cornerGeneral2d)
    {
        int2 examinedDistanceVector = math.abs(examinedGeneral2d - startGeneral2d);
        int2 cornerDistanceVector = math.abs(cornerGeneral2d - startGeneral2d);
        bool isExaminedXCloser = examinedDistanceVector.x < cornerDistanceVector.x;
        bool isExaminedYCloser = examinedDistanceVector.y < cornerDistanceVector.y;
        return isExaminedXCloser ^ isExaminedYCloser;
    }

    private enum CornerBlockDirection : byte
    {
        None = 0,
        Right = 1,
        Left = 2,
    };
    private struct NewIndexData
    {
        public int Index;
        public int NewVerDif;
        public int NewHorDif;
        public CornerBlockDirection CornerBlockDirection;
    }
}
