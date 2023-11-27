﻿using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using System.Security.Cryptography;
using UnityEngine;
using Unity.Burst;
using Unity.VisualScripting;
using static Unity.Burst.Intrinsics.X86;

[BurstCompile]
public struct LOSIntegrationJob : IJob
{
    public int2 Target;
    public float TileSize;
    public float MaxLOSRange;
    public int FieldColAmount;
    public int FieldRowAmount;
    public int SectorColAmount;
    public int SectorMatrixColAmount;
    public int SectorMatrixRowAmount;
    public int SectorTileAmount;

    [ReadOnly] public NativeArray<UnsafeList<byte>> Costs;
    public UnsafeList<int> SectorToPicked;
    public NativeArray<IntegrationTile> IntegrationField;
    public void Execute()
    {
        int sectorColAmount = SectorColAmount;
        int sectorTileAmount = SectorTileAmount;
        int sectorMatrixColAmount = SectorMatrixColAmount;
        float maxLosRange = MaxLOSRange;
        int2 targetGeneral2d = Target;
        UnsafeList<int> sectorToPickedTable = SectorToPicked;
        NativeQueue<LocalIndex1d> integrationQueue = new NativeQueue<LocalIndex1d>(Allocator.Temp);
        NativeArray<UnsafeList<byte>> costs = Costs;
        NativeArray<IntegrationTile> integrationField = IntegrationField;


        //LOOKUP TABLE
        int curLocal1d;
        int curSector1d;
        int curSectorMark;
        int2 curGeneral2d;
        IntegrationTile curTile;

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

        int2 nGeneral2d;
        int2 eGeneral2d;
        int2 sGeneral2d;
        int2 wGeneral2d;
        int2 neGeneral2d;
        int2 seGeneral2d;
        int2 swGeneral2d;
        int2 nwGeneral2d;

        int nSectorMark;
        int eSectorMark;
        int sSectorMark;
        int wSectorMark;
        int neSectorMark;
        int seSectorMark;
        int swSectorMark;
        int nwSectorMark;

        bool nUnwalkable;
        bool eUnwalkable;
        bool sUnwalkable;
        bool wUnwalkable;
        bool neUnwalkable;
        bool seUnwalkable;
        bool swUnwalkable;
        bool nwUnwalkable;

        float nDistance;
        float eDistance;
        float sDistance;
        float wDistance;

        IntegrationTile nTile;
        IntegrationTile eTile;
        IntegrationTile sTile;
        IntegrationTile wTile;
        IntegrationTile neTile;
        IntegrationTile seTile;
        IntegrationTile swTile;
        IntegrationTile nwTile;

        //INNITIAL STEP
        int2 targetSector2d = FlowFieldUtilities.GetSector2D(Target, sectorColAmount);
        int2 targetSectorStart = FlowFieldUtilities.GetSectorStartIndex(targetSector2d, sectorColAmount);
        int targetLocal1d = FlowFieldUtilities.GetLocal1D(Target, targetSectorStart, sectorColAmount);
        int targetSector1d = FlowFieldUtilities.To1D(targetSector2d, sectorMatrixColAmount);
        LocalIndex1d targetLocal = new LocalIndex1d()
        {
            sector = targetSector1d,
            index = targetLocal1d,
        };
        integrationQueue.Enqueue(targetLocal);
        IntegrationField[sectorToPickedTable[targetSector1d] + targetLocal1d] = new IntegrationTile()
        {
            Cost = float.MaxValue,
            Mark = IntegrationMark.LOSPass,
        };

        //LOOP
        while (!integrationQueue.IsEmpty())
        {
            LocalIndex1d curIndex = integrationQueue.Dequeue();
            SetLookupTable(curIndex);
            if (TestAllLOSC())
            {
                RefreshOrthogonalTiles();
            }
            EnqueueNeighbours();
        }

        void SetLookupTable(LocalIndex1d curIndex)
        {
            curLocal1d = curIndex.index;
            curSector1d = curIndex.sector;

            nLocal1d = curLocal1d + sectorColAmount;
            eLocal1d = curLocal1d + 1;
            sLocal1d = curLocal1d - sectorColAmount;
            wLocal1d = curLocal1d - 1;
            neLocal1d = nLocal1d + 1;
            seLocal1d = sLocal1d + 1;
            swLocal1d = sLocal1d - 1;
            nwLocal1d = nLocal1d - 1;

            //OVERFLOWS
            bool nLocalOverflow = nLocal1d >= sectorTileAmount;
            bool eLocalOverflow = (eLocal1d % sectorColAmount) == 0;
            bool sLocalOverflow = sLocal1d < 0;
            bool wLocalOverflow = (curLocal1d % sectorColAmount) == 0;

            //SECTOR INDICIES
            nSector1d = math.select(curSector1d, curSector1d + sectorMatrixColAmount, nLocalOverflow);
            eSector1d = math.select(curSector1d, curSector1d + 1, eLocalOverflow);
            sSector1d = math.select(curSector1d, curSector1d - sectorMatrixColAmount, sLocalOverflow);
            wSector1d = math.select(curSector1d, curSector1d - 1, wLocalOverflow);
            neSector1d = math.select(curSector1d, curSector1d + sectorMatrixColAmount, nLocalOverflow);
            neSector1d = math.select(neSector1d, neSector1d + 1, eLocalOverflow);
            seSector1d = math.select(curSector1d, curSector1d - sectorMatrixColAmount, sLocalOverflow);
            seSector1d = math.select(seSector1d, seSector1d + 1, eLocalOverflow);
            swSector1d = math.select(curSector1d, curSector1d - sectorMatrixColAmount, sLocalOverflow);
            swSector1d = math.select(swSector1d, swSector1d - 1, wLocalOverflow);
            nwSector1d = math.select(curSector1d, curSector1d + sectorMatrixColAmount, nLocalOverflow);
            nwSector1d = math.select(nwSector1d, nwSector1d - 1, wLocalOverflow);


            nLocal1d = math.select(nLocal1d, curLocal1d - (sectorColAmount * sectorColAmount - sectorColAmount), nLocalOverflow);
            eLocal1d = math.select(eLocal1d, curLocal1d - sectorColAmount + 1, eLocalOverflow);
            sLocal1d = math.select(sLocal1d, curLocal1d + (sectorColAmount * sectorColAmount - sectorColAmount), sLocalOverflow);
            wLocal1d = math.select(wLocal1d, curLocal1d + sectorColAmount - 1, wLocalOverflow);
            neLocal1d = math.select(neLocal1d, neLocal1d - (sectorColAmount * sectorColAmount), nLocalOverflow);
            neLocal1d = math.select(neLocal1d, neLocal1d - sectorColAmount, eLocalOverflow);
            seLocal1d = math.select(seLocal1d, seLocal1d + (sectorColAmount * sectorColAmount), sLocalOverflow);
            seLocal1d = math.select(seLocal1d, seLocal1d - sectorColAmount, eLocalOverflow);
            swLocal1d = math.select(swLocal1d, swLocal1d + (sectorColAmount * sectorColAmount), sLocalOverflow);
            swLocal1d = math.select(swLocal1d, swLocal1d + sectorColAmount, wLocalOverflow);
            nwLocal1d = math.select(nwLocal1d, nwLocal1d - (sectorColAmount * sectorColAmount), nLocalOverflow);
            nwLocal1d = math.select(nwLocal1d, nwLocal1d + sectorColAmount, wLocalOverflow);

            //GENERAL 2D
            curGeneral2d = FlowFieldUtilities.GetGeneral2d(curLocal1d, curSector1d, sectorMatrixColAmount, sectorColAmount);
            nGeneral2d = FlowFieldUtilities.GetGeneral2d(nLocal1d, nSector1d, sectorMatrixColAmount, sectorColAmount);
            eGeneral2d = FlowFieldUtilities.GetGeneral2d(eLocal1d, eSector1d, sectorMatrixColAmount, sectorColAmount);
            sGeneral2d = FlowFieldUtilities.GetGeneral2d(sLocal1d, sSector1d, sectorMatrixColAmount, sectorColAmount);
            wGeneral2d = FlowFieldUtilities.GetGeneral2d(wLocal1d, wSector1d, sectorMatrixColAmount, sectorColAmount);
            neGeneral2d = FlowFieldUtilities.GetGeneral2d(neLocal1d, neSector1d, sectorMatrixColAmount, sectorColAmount);
            seGeneral2d = FlowFieldUtilities.GetGeneral2d(seLocal1d, seSector1d, sectorMatrixColAmount, sectorColAmount);
            swGeneral2d = FlowFieldUtilities.GetGeneral2d(swLocal1d, swSector1d, sectorMatrixColAmount, sectorColAmount);
            nwGeneral2d = FlowFieldUtilities.GetGeneral2d(nwLocal1d, nwSector1d, sectorMatrixColAmount, sectorColAmount);

            //SECTOR MARKS
            curSectorMark = sectorToPickedTable[curSector1d];
            nSectorMark = sectorToPickedTable[nSector1d];
            eSectorMark = sectorToPickedTable[eSector1d];
            sSectorMark = sectorToPickedTable[sSector1d];
            wSectorMark = sectorToPickedTable[wSector1d];
            neSectorMark = sectorToPickedTable[neSector1d];
            seSectorMark = sectorToPickedTable[seSector1d];
            swSectorMark = sectorToPickedTable[swSector1d];
            nwSectorMark = sectorToPickedTable[nwSector1d];

            //TILES
            curTile = integrationField[curSectorMark + curLocal1d];
            nTile = integrationField[nSectorMark + nLocal1d];
            eTile = integrationField[eSectorMark + eLocal1d];
            sTile = integrationField[sSectorMark + sLocal1d];
            wTile = integrationField[wSectorMark + wLocal1d];
            neTile = integrationField[neSectorMark + neLocal1d];
            seTile = integrationField[seSectorMark + seLocal1d];
            swTile = integrationField[swSectorMark + swLocal1d];
            nwTile = integrationField[nwSectorMark + nwLocal1d];

            //COSTS
            UnsafeList<byte> nCosts = costs[nSector1d];
            UnsafeList<byte> eCosts = costs[eSector1d];
            UnsafeList<byte> sCosts = costs[sSector1d];
            UnsafeList<byte> wCosts = costs[wSector1d];
            UnsafeList<byte> neCosts = costs[neSector1d];
            UnsafeList<byte> seCosts = costs[seSector1d];
            UnsafeList<byte> swCosts = costs[swSector1d];
            UnsafeList<byte> nwCosts = costs[nwSector1d];
            nUnwalkable = nCosts[nLocal1d] == byte.MaxValue;
            eUnwalkable = eCosts[eLocal1d] == byte.MaxValue;
            sUnwalkable = sCosts[sLocal1d] == byte.MaxValue;
            wUnwalkable = wCosts[wLocal1d] == byte.MaxValue;
            neUnwalkable = neCosts[neLocal1d] == byte.MaxValue;
            seUnwalkable = seCosts[seLocal1d] == byte.MaxValue;
            swUnwalkable = swCosts[swLocal1d] == byte.MaxValue;
            nwUnwalkable = nwCosts[nwLocal1d] == byte.MaxValue;

            //TILES
            nTile = integrationField[nSectorMark + nLocal1d];
            eTile = integrationField[eSectorMark + eLocal1d];
            sTile = integrationField[sSectorMark + sLocal1d];
            wTile = integrationField[wSectorMark + wLocal1d];

            //TILE DISTANCES
            nDistance = GetDistanceFromStart(nGeneral2d);
            eDistance = GetDistanceFromStart(eGeneral2d);
            sDistance = GetDistanceFromStart(sGeneral2d);
            wDistance = GetDistanceFromStart(wGeneral2d);
        }

        void RefreshOrthogonalTiles()
        {
            nTile = integrationField[nSectorMark + nLocal1d];
            eTile = integrationField[eSectorMark + eLocal1d];
            sTile = integrationField[sSectorMark + sLocal1d];
            wTile = integrationField[wSectorMark + wLocal1d];
        }

        void EnqueueNeighbours()
        {
            bool nEnqueueable = nTile.Mark == IntegrationMark.None && !nUnwalkable && nSectorMark != 0 && nDistance <= maxLosRange;
            bool eEnqueueable = eTile.Mark == IntegrationMark.None && !eUnwalkable && eSectorMark != 0 && eDistance <= maxLosRange;
            bool sEnqueueable = sTile.Mark == IntegrationMark.None && !sUnwalkable && sSectorMark != 0 && sDistance <= maxLosRange;
            bool wEnqueueable = wTile.Mark == IntegrationMark.None && !wUnwalkable && wSectorMark != 0 && wDistance <= maxLosRange;

            if (nEnqueueable)
            {
                integrationField[nSectorMark + nLocal1d] = new IntegrationTile()
                {
                    Cost = nTile.Cost,
                    Mark = nTile.Mark | IntegrationMark.LOSPass,
                };
                integrationQueue.Enqueue(new LocalIndex1d(nLocal1d, nSector1d));
            }
            if (eEnqueueable)
            {
                integrationField[eSectorMark + eLocal1d] = new IntegrationTile()
                {
                    Cost = eTile.Cost,
                    Mark = eTile.Mark | IntegrationMark.LOSPass,
                };
                integrationQueue.Enqueue(new LocalIndex1d(eLocal1d, eSector1d));
            }
            if (sEnqueueable)
            {
                integrationField[sSectorMark + sLocal1d] = new IntegrationTile()
                {
                    Cost = sTile.Cost,
                    Mark = sTile.Mark | IntegrationMark.LOSPass,
                };
                integrationQueue.Enqueue(new LocalIndex1d(sLocal1d, sSector1d));
            }
            if (wEnqueueable)
            {
                integrationField[wSectorMark + wLocal1d] = new IntegrationTile()
                {
                    Cost = wTile.Cost,
                    Mark = wTile.Mark | IntegrationMark.LOSPass,
                };
                integrationQueue.Enqueue(new LocalIndex1d(wLocal1d, wSector1d));
            }
        }
        
        float GetDistanceFromStart(int2 general2d)
        {
            int2 dif = math.abs(targetGeneral2d - general2d);
            int minComponent = math.min(dif.x, dif.y);
            int maxComponent = math.max(dif.x, dif.y);
            return minComponent * 1.4f + (maxComponent - minComponent);
        }

        bool TestAllLOSC()
        {
            bool neCorner = neUnwalkable && !nUnwalkable && !eUnwalkable;
            bool seCorner = seUnwalkable && !sUnwalkable && !eUnwalkable;
            bool swCorner = swUnwalkable && !sUnwalkable && !wUnwalkable;
            bool nwCorner = nwUnwalkable && !nUnwalkable && !wUnwalkable;
            bool cornerDetected = false;
            if (neCorner)
            {
                bool test = TestLOSC(neGeneral2d);
                cornerDetected = cornerDetected || test; 
            }
            if (seCorner)
            {
                bool test = TestLOSC(seGeneral2d);
                cornerDetected = cornerDetected || test;
            }
            if (swCorner)
            {
                bool test = TestLOSC(swGeneral2d);
                cornerDetected = cornerDetected || test;
            }
            if (nwCorner)
            {
                bool test = TestLOSC(nwGeneral2d);
                cornerDetected = cornerDetected || test;
            }
            return cornerDetected;
        }
        bool TestLOSC(int2 pickedUnwalkableGeneral2d)
        {
            int2 unwalkableToTarget = targetGeneral2d - pickedUnwalkableGeneral2d;
            int2 walkableToTarget = targetGeneral2d - curGeneral2d;
            int2 unwalkableToTargetAbs = math.abs(unwalkableToTarget);
            int2 walkableToTargetAbs = math.abs(walkableToTarget);
            int2 toTargetAbsDif = unwalkableToTargetAbs - walkableToTargetAbs;
            if (toTargetAbsDif.x * toTargetAbsDif.y >= 0) { return false; }

            int2 unwalkableToWalkableChange = curGeneral2d - pickedUnwalkableGeneral2d;
            int2 closerSideOfCorner = math.select(pickedUnwalkableGeneral2d + new int2(0, unwalkableToWalkableChange.y), pickedUnwalkableGeneral2d + new int2(unwalkableToWalkableChange.x, 0), walkableToTargetAbs.x < unwalkableToTargetAbs.x);
            int2 fartherSideOfCorner = math.select(pickedUnwalkableGeneral2d + new int2(0, unwalkableToWalkableChange.y), pickedUnwalkableGeneral2d + new int2(unwalkableToWalkableChange.x, 0), walkableToTargetAbs.x > unwalkableToTargetAbs.x);
            int2 fartherSideOfCornerChange = fartherSideOfCorner - targetGeneral2d;
            int2 fartherSideOfCornerChangeAbs = math.abs(fartherSideOfCornerChange);
            int2 lineStep = (closerSideOfCorner - targetGeneral2d) * 2 + new int2(fartherSideOfCornerChange.x / fartherSideOfCornerChangeAbs.x, fartherSideOfCornerChange.y / fartherSideOfCornerChangeAbs.y);
            float stepLength = GetLength(lineStep);
            int stepCount = (int)math.ceil(maxLosRange / stepLength);
            Quadrant quadrant = Quadrant.Q1;
            if (lineStep.x >= 0 && lineStep.y >= 0) { quadrant = Quadrant.Q1; }
            else if (lineStep.x <= 0 && lineStep.y >= 0) { quadrant = Quadrant.Q2; }
            else if (lineStep.x <= 0 && lineStep.y <= 0) { quadrant = Quadrant.Q3; }
            else { quadrant = Quadrant.Q4; }


            RunBresenham(targetGeneral2d, fartherSideOfCorner, lineStep * stepCount, quadrant);
            return true;
        }
        float GetLength(int2 change)
        {
            change = math.abs(change);
            int minComponent = math.min(change.x, change.y);
            int maxComponent = math.max(change.x, change.y);
            return minComponent * 1.4f + (maxComponent - minComponent);
        }
        void RunBresenham(int2 lineStartPoint, int2 calculationStartPoint, int2 deltaXY, Quadrant quadrant)
        {
            int2 offset = lineStartPoint;
            int2 calculationLocalStart = math.abs(calculationStartPoint - lineStartPoint);
            int2 localDeltaXY = math.abs(deltaXY);
            bool slopeMagnitudeGreaterThanOne = localDeltaXY.x < localDeltaXY.y;
            int2 reflection = 0;
            switch (quadrant)
            {
                case Quadrant.Q1:
                    reflection = new int2(1, 1);
                    break;
                case Quadrant.Q2:
                    reflection = new int2(-1, 1);
                    break;
                case Quadrant.Q3:
                    reflection = new int2(-1, -1);
                    break;
                case Quadrant.Q4:
                    reflection = new int2(1, -1);
                    break;
            }
            if (!slopeMagnitudeGreaterThanOne)
            {
                //CONSTANTS
                int deltaX = localDeltaXY.x;
                int deltaY = localDeltaXY.y;
                int doubleDeltaX = deltaX * 2;
                int doubleDeltaY = deltaY * 2;
                int decisionDelta = doubleDeltaY - deltaX;

                //LOOP
                for (int x = calculationLocalStart.x, y = calculationLocalStart.y; x <= localDeltaXY.x; x++)
                {
                    //MARK
                    int2 general2d = new int2(x, y) * reflection + offset;
                    LocalIndex1d localIndex = FlowFieldUtilities.GetLocal1D(general2d, sectorColAmount, sectorMatrixColAmount);
                    if (costs[localIndex.sector][localIndex.index] == byte.MaxValue) { break; }
                    if (GetDistanceFromStart(general2d) > maxLosRange) { break; }
                    int sectorMark = sectorToPickedTable[localIndex.sector];
                    if (sectorMark == 0) { continue; }
                    IntegrationTile tile = integrationField[sectorMark + localIndex.index];
                    if ((tile.Mark & IntegrationMark.LOSBlock) == IntegrationMark.LOSBlock) { break; }
                    integrationField[sectorMark + localIndex.index] = new IntegrationTile()
                    {
                        Cost = tile.Cost,
                        Mark = tile.Mark | IntegrationMark.LOSBlock,
                    };
                    //ITERATE
                    if (decisionDelta < 0)
                    {
                        decisionDelta += doubleDeltaY;
                    }
                    else
                    {
                        y++;
                        decisionDelta += doubleDeltaY - doubleDeltaX;
                    }
                }
            }
            else
            {
                //CONSTANTS
                int deltaX = localDeltaXY.y;
                int deltaY = localDeltaXY.x;
                int doubleDeltaX = deltaX * 2;
                int doubleDeltaY = deltaY * 2;
                int decisionDelta = doubleDeltaY - deltaX;

                //LOOP
                for (int x = calculationLocalStart.y, y = calculationLocalStart.x; x <= deltaX; x++)
                {

                    //MARK
                    int2 general2d = new int2(y, x) * reflection + offset;
                    LocalIndex1d localIndex = FlowFieldUtilities.GetLocal1D(general2d, sectorColAmount, sectorMatrixColAmount);
                    if (costs[localIndex.sector][localIndex.index] == byte.MaxValue) { break; }
                    if (GetDistanceFromStart(general2d) > maxLosRange) { break; }
                    int sectorMark = sectorToPickedTable[localIndex.sector];
                    if (sectorMark == 0) { continue; }
                    IntegrationTile tile = integrationField[sectorMark + localIndex.index];
                    if ((tile.Mark & IntegrationMark.LOSBlock) == IntegrationMark.LOSBlock) { break; }
                    integrationField[sectorMark + localIndex.index] = new IntegrationTile()
                    {
                        Cost = tile.Cost,
                        Mark = tile.Mark | IntegrationMark.LOSBlock,
                    };
                    //ITERATE
                    if (decisionDelta < 0)
                    {
                        decisionDelta += doubleDeltaY;
                    }
                    else
                    {
                        y++;
                        decisionDelta += doubleDeltaY - doubleDeltaX;
                    }
                }
            }
        }
    }

    
}   
enum Quadrant : byte
{
    Q1 = 0,
    Q2 = 1,
    Q3 = 2,
    Q4 = 3
};