using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Assertions.Must;

[BurstCompile]
public struct LOSJob : IJob
{
    public int2 Target;
    public float TileSize;
    public int FieldColAmount;
    public int FieldRowAmount;
    public int SectorTileAmount;
    public int SectorMatrixColAmount;
    public int SectorMatrixRowAmount;
    [ReadOnly] public NativeArray<byte> Costs;
    [ReadOnly] public NativeArray<UnsafeList<LocalDirectionData1d>> Directions;
    public NativeArray<int> SectorMarks;
    public NativeList<IntegrationFieldSector> IntegrationField;
    public NativeQueue<LocalIndex1d> IntegrationQueue;
    public NativeQueue<LocalIndex1d> BlockedWaveFronts;
    public void Execute()
    {
        //DATA
        int sectorTileAmount = SectorTileAmount;
        int sectorMatrixColAmount = SectorMatrixColAmount;
        int fieldColAmount = FieldColAmount;
        int fieldRowAmount = FieldRowAmount;
        int field1dSize = fieldColAmount * FieldRowAmount;
        float tileSize = TileSize;
        int2 target = Target;
        NativeArray<int> sectorMarks = SectorMarks;
        NativeList<IntegrationFieldSector> integrationField = IntegrationField;
        NativeQueue<LocalIndex1d> integrationQueue = IntegrationQueue;
        NativeArray<byte> costs = Costs;
        NativeArray<UnsafeList<LocalDirectionData1d>> directions = Directions;
        NativeQueue<LocalIndex1d> blockedWaveFronts = BlockedWaveFronts;

        ///////CURRENT INDEX LOOKUP TABLE////////
        /////////////////////////////////////////
        LocalDirectionData1d direction;
        int curLocal1d;
        int curSector1d;
        int2 nLocal2d;
        int2 eLocal2d;
        int2 sLocal2d;
        int2 wLocal2d;
        int2 nSector2d;
        int2 eSector2d;
        int2 sSector2d;
        int2 wSector2d;
        int nGeneral1d;
        int eGeneral1d;
        int sGeneral1d;
        int wGeneral1d;
        byte nCost;
        byte eCost;
        byte sCost;
        byte wCost;
        float nIntCost;
        float eIntCost;
        float sIntCost;
        float wIntCost;
        float neIntCost;
        float seIntCost;
        float swIntCost;
        float nwIntCost;
        bool nRelevant;
        bool eRelevant;
        bool sRelevant;
        bool wRelevant;
        UnsafeList<IntegrationTile> nSector;
        UnsafeList<IntegrationTile> eSector;
        UnsafeList<IntegrationTile> sSector;
        UnsafeList<IntegrationTile> wSector;
        UnsafeList<IntegrationTile> neSector;
        UnsafeList<IntegrationTile> seSector;
        UnsafeList<IntegrationTile> swSector;
        UnsafeList<IntegrationTile> nwSector;
        ///////////////////////////////////////////

        //SET TARGET INDEX
        int2 targetSector2d = GetSectorIndex(Target);
        int targetSector1d = To1D(targetSector2d, SectorMatrixColAmount);
        int2 targetSectorStartIndex = new int2(targetSector2d.x * SectorTileAmount, targetSector2d.y * SectorTileAmount);
        int2 targetLocal2d = GetLocalIndex(Target, targetSectorStartIndex);
        int targetLocal1d = To1D(targetLocal2d, SectorTileAmount);

        SetLookupTable(targetLocal1d, targetSector1d);
        UnsafeList<IntegrationTile> targetSector = IntegrationField[SectorMarks[targetSector1d]].integrationSector;
        IntegrationTile targetTile = targetSector[targetLocal1d];
        targetTile.Cost = 0f;
        targetTile.Mark = IntegrationMark.LOSPass;
        targetSector[targetLocal1d] = targetTile;
        LookForLOSC();
        EnqueueNeighbours();
        while (!integrationQueue.IsEmpty())
        {
            LocalIndex1d curIndex = integrationQueue.Dequeue();
            int curSectorMark = SectorMarks[curIndex.sector];
            UnsafeList<IntegrationTile> integrationSector = integrationField[curSectorMark].integrationSector;
            IntegrationTile curTile = integrationSector[curIndex.index];
            if(curTile.Mark == IntegrationMark.LOSBlock) { continue; }
            SetLookupTable(curIndex.index, curIndex.sector);
            curTile.Mark = IntegrationMark.LOSPass;
            curTile.Cost = GetCost();
            integrationSector[curIndex.index] = curTile;
            LookForLOSC();
            EnqueueNeighbours();
        }
        void SetLookupTable(int curLocal1d, int curSector1d)
        {
            direction = directions[curSector1d][curLocal1d];
            //LOCAL 2D INDICIES
            nLocal2d = To2D(direction.n, sectorTileAmount);
            eLocal2d = To2D(direction.e, sectorTileAmount);
            sLocal2d = To2D(direction.s, sectorTileAmount);
            wLocal2d = To2D(direction.w, sectorTileAmount);

            //SECTOR 2D INDICIES
            nSector2d = To2D(direction.nSector, sectorMatrixColAmount);
            eSector2d = To2D(direction.eSector, sectorMatrixColAmount);
            sSector2d = To2D(direction.sSector, sectorMatrixColAmount);
            wSector2d = To2D(direction.wSector, sectorMatrixColAmount);

            //GENERAL 1D INDICIES
            nGeneral1d = GetGeneral1d(nLocal2d, nSector2d);
            eGeneral1d = GetGeneral1d(eLocal2d, eSector2d);
            sGeneral1d = GetGeneral1d(sLocal2d, sSector2d);
            wGeneral1d = GetGeneral1d(wLocal2d, wSector2d);

            //SECTOR MARKS
            int nSectorMark = sectorMarks[direction.nSector];
            int eSectorMark = sectorMarks[direction.eSector];
            int sSectorMark = sectorMarks[direction.sSector];
            int wSectorMark = sectorMarks[direction.wSector];
            int neSectorMark = sectorMarks[direction.neSector];
            int seSectorMark = sectorMarks[direction.seSector];
            int swSectorMark = sectorMarks[direction.swSector];
            int nwSectorMark = sectorMarks[direction.nwSector];

            //SECTORS
            nSector = integrationField[nSectorMark].integrationSector;
            eSector = integrationField[eSectorMark].integrationSector;
            sSector = integrationField[sSectorMark].integrationSector;
            wSector = integrationField[wSectorMark].integrationSector;
            neSector = integrationField[neSectorMark].integrationSector;
            seSector = integrationField[seSectorMark].integrationSector;
            swSector = integrationField[swSectorMark].integrationSector;
            nwSector = integrationField[nwSectorMark].integrationSector;

            //TILES
            IntegrationTile nTile = new IntegrationTile(float.MaxValue, IntegrationMark.None);
            IntegrationTile eTile = new IntegrationTile(float.MaxValue, IntegrationMark.None);
            IntegrationTile sTile = new IntegrationTile(float.MaxValue, IntegrationMark.None);
            IntegrationTile wTile = new IntegrationTile(float.MaxValue, IntegrationMark.None);
            IntegrationTile neTile = new IntegrationTile(float.MaxValue, IntegrationMark.None);
            IntegrationTile seTile = new IntegrationTile(float.MaxValue, IntegrationMark.None);
            IntegrationTile swTile = new IntegrationTile(float.MaxValue, IntegrationMark.None);
            IntegrationTile nwTile = new IntegrationTile(float.MaxValue, IntegrationMark.None);

            if (nSectorMark != 0) { nTile = nSector[direction.n]; }
            if (eSectorMark != 0) { eTile = eSector[direction.e]; }
            if (sSectorMark != 0) { sTile = sSector[direction.s]; }
            if (wSectorMark != 0) { wTile = wSector[direction.w]; }
            if (neSectorMark != 0) { neTile = neSector[direction.ne]; }
            if (seSectorMark != 0) { seTile = seSector[direction.se]; }
            if (swSectorMark != 0) { swTile = swSector[direction.sw]; }
            if (nwSectorMark != 0) { nwTile = nwSector[direction.nw]; }

            //RELEVANCIES
            nRelevant = nSectorMark != 0 && nTile.Mark == IntegrationMark.None;
            eRelevant = eSectorMark != 0 && eTile.Mark == IntegrationMark.None;
            sRelevant = sSectorMark != 0 && sTile.Mark == IntegrationMark.None;
            wRelevant = wSectorMark != 0 && wTile.Mark == IntegrationMark.None;

            //COSTS
            nCost = costs[nGeneral1d];
            eCost = costs[eGeneral1d];
            sCost = costs[sGeneral1d];
            wCost = costs[wGeneral1d];

            //INTEGRATED COSTS
            nIntCost = nTile.Cost;
            eIntCost = eTile.Cost;
            sIntCost = sTile.Cost;
            wIntCost = wTile.Cost;
            neIntCost = neTile.Cost;
            seIntCost = seTile.Cost;
            swIntCost = swTile.Cost;
            nwIntCost = nwTile.Cost;
        }
        void EnqueueNeighbours()
        {
            bool nEnqueueable = nRelevant && nCost != byte.MaxValue;
            bool eEnqueueable = eRelevant && eCost != byte.MaxValue;
            bool sEnqueueable = sRelevant && sCost != byte.MaxValue;
            bool wEnqueueable = wRelevant && wCost != byte.MaxValue;
            if (nEnqueueable)
            {
                integrationQueue.Enqueue(new LocalIndex1d(direction.n, direction.nSector));
                IntegrationTile tile = nSector[direction.n];
                tile.Mark = IntegrationMark.Awaiting;
                nSector[direction.n] = tile;

            }
            if (eEnqueueable)
            {
                integrationQueue.Enqueue(new LocalIndex1d(direction.e, direction.eSector));
                IntegrationTile tile = eSector[direction.e];
                tile.Mark = IntegrationMark.Awaiting;
                eSector[direction.e] = tile;
            }
            if (sEnqueueable)
            {
                integrationQueue.Enqueue(new LocalIndex1d(direction.s, direction.sSector));
                IntegrationTile tile = sSector[direction.s];
                tile.Mark = IntegrationMark.Awaiting;
                sSector[direction.s] = tile;
            }
            if (wEnqueueable)
            {
                integrationQueue.Enqueue(new LocalIndex1d(direction.w, direction.wSector));
                IntegrationTile tile = wSector[direction.w];
                tile.Mark = IntegrationMark.Awaiting;
                wSector[direction.w] = tile;
            }
        }
        float GetCost()
        {
            float costToReturn = float.MaxValue;
            float nCost = nIntCost + 1f;
            float eCost = eIntCost + 1f;
            float sCost = sIntCost + 1f;
            float wCost = wIntCost + 1f;
            float neCost = neIntCost + 1.4f;
            float seCost = seIntCost + 1.4f;
            float swCost = swIntCost + 1.4f;
            float nwCost = nwIntCost + 1.4f;

            costToReturn = math.select(costToReturn, nCost, nCost < costToReturn);
            costToReturn = math.select(costToReturn, eCost, eCost < costToReturn);
            costToReturn = math.select(costToReturn, sCost, sCost < costToReturn);
            costToReturn = math.select(costToReturn, wCost, wCost < costToReturn);
            costToReturn = math.select(costToReturn, neCost, neCost < costToReturn);
            costToReturn = math.select(costToReturn, seCost, seCost < costToReturn);
            costToReturn = math.select(costToReturn, swCost, swCost < costToReturn);
            costToReturn = math.select(costToReturn, nwCost, nwCost < costToReturn);
            return costToReturn;
        }
        void LookForLOSC()
        {
            if (nRelevant && nCost == byte.MaxValue)
            {
                ApplyLOSBlockIfLOSCorner(nGeneral1d, direction.n, direction.nSector);
            }
            if (eRelevant && eCost == byte.MaxValue)
            {
                ApplyLOSBlockIfLOSCorner(eGeneral1d, direction.e, direction.eSector);
            }
            if (sRelevant && sCost == byte.MaxValue)
            {
                ApplyLOSBlockIfLOSCorner(sGeneral1d, direction.s, direction.sSector);
            }
            if (wRelevant && wCost == byte.MaxValue)
            {
                ApplyLOSBlockIfLOSCorner(wGeneral1d, direction.w, direction.wSector);
            }

            void ApplyLOSBlockIfLOSCorner(int generalIndex1d, int localIndex1d, int sectorIndex1d)
            {
                //NEIGHBOUR LOOKUP TABLE
                int n = generalIndex1d + fieldColAmount;
                int e = generalIndex1d + 1;
                int s = generalIndex1d - fieldColAmount;
                int w = generalIndex1d - 1;
                int ne = n + 1;
                int se = s + 1;
                int sw = s - 1;
                int nw = n - 1;

                //HANDLE OVERFLOWS
                bool nOverflow = n >= field1dSize;
                bool eOverflow = (e % fieldColAmount) == 0;
                bool sOverflow = s < 0;
                bool wOverflow = (generalIndex1d % fieldColAmount) == 0;
                n = math.select(n, generalIndex1d, nOverflow);
                e = math.select(e, generalIndex1d, eOverflow);
                s = math.select(s, generalIndex1d, sOverflow);
                w = math.select(w, generalIndex1d, wOverflow);
                ne = math.select(ne, generalIndex1d, nOverflow || eOverflow);
                se = math.select(se, generalIndex1d, sOverflow || eOverflow);
                sw = math.select(sw, generalIndex1d, sOverflow || wOverflow);
                nw = math.select(nw, generalIndex1d, nOverflow || wOverflow);

                //COSTS
                byte nCost = costs[n];
                byte eCost = costs[e];
                byte sCost = costs[s];
                byte wCost = costs[w];
                byte neCost = costs[ne];
                byte seCost = costs[se];
                byte swCost = costs[sw];
                byte nwCost = costs[nw];

                //IS CORNER?
                bool isCornerFromNE = neCost != byte.MaxValue && nCost != byte.MaxValue && eCost != byte.MaxValue;
                bool isCornerFromSE = seCost != byte.MaxValue && sCost != byte.MaxValue && eCost != byte.MaxValue;
                bool isCornerFromSW = swCost != byte.MaxValue && sCost != byte.MaxValue && wCost != byte.MaxValue;
                bool isCornerFromNW = nwCost != byte.MaxValue && nCost != byte.MaxValue && wCost != byte.MaxValue;
                if (!IsCorner()) { return; }

                //CORNER LOOKUP TABLE
                int2 source = target;
                int2 cornerIndex = To2D(generalIndex1d, fieldColAmount);
                int2 cornerDistance = new int2(source.x - cornerIndex.x, source.y - cornerIndex.y);
                int2 absCornerDistance = new int2(math.abs(cornerDistance.x), math.abs(cornerDistance.y));
                float2 cornerPos = new float2(cornerIndex.x * tileSize + tileSize / 2, cornerIndex.y * tileSize + tileSize / 2);
                float2 waveFrontTilePos = new float2(source.x * tileSize + tileSize / 2, source.y * tileSize + tileSize / 2);

                //EVALUATE FOR EACH CORNER DIRECTION
                if (isCornerFromNE)
                {
                    int2 neIndex2 = To2D(ne, fieldColAmount);
                    int2 neDistance = new int2(source.x - neIndex2.x, source.y - neIndex2.y);
                    int2 absNeDistance = new int2(math.abs(neDistance.x), math.abs(neDistance.y));
                    int2 distanceDifference = new int2(absCornerDistance.x - absNeDistance.x, absCornerDistance.y - absNeDistance.y);
                    if (distanceDifference.y * distanceDifference.x < 0) //if losc
                    {
                        UnsafeList<IntegrationTile> integrationSector = integrationField[sectorMarks[sectorIndex1d]].integrationSector;
                        IntegrationTile cornerTile = integrationSector[localIndex1d];
                        cornerTile.Mark = IntegrationMark.LOSC;
                        integrationSector[localIndex1d] = cornerTile;
                        int2 nIndex = To2D(n, fieldColAmount);
                        int2 eIndex = To2D(e, fieldColAmount);
                        int2 divergent = distanceDifference.x > 0 ? nIndex : eIndex;
                        float2 loscPosition = cornerPos + new float2(tileSize / 2, tileSize / 2);
                        float2 sourcePosition = waveFrontTilePos + new float2(-tileSize / 2, -tileSize / 2);
                        NativeList<int2> blockOffsets = GetOffsets(sourcePosition, loscPosition, out int2 stepAmount);
                        int2 step = divergent;
                        SetLOSBlocks(blockOffsets, step, stepAmount);
                    }
                }
                if (isCornerFromSE)
                {
                    int2 seIndex2 = To2D(se, fieldColAmount);
                    int2 seDistance = new int2(source.x - seIndex2.x, source.y - seIndex2.y);
                    int2 absSeDistance = new int2(math.abs(seDistance.x), math.abs(seDistance.y));
                    int2 distanceDifference = new int2(absCornerDistance.x - absSeDistance.x, absCornerDistance.y - absSeDistance.y);
                    if (distanceDifference.y * distanceDifference.x < 0) //if losc
                    {
                        UnsafeList<IntegrationTile> integrationSector = integrationField[sectorMarks[sectorIndex1d]].integrationSector;
                        IntegrationTile cornerTile = integrationSector[localIndex1d];
                        cornerTile.Mark = IntegrationMark.LOSC;
                        integrationSector[localIndex1d] = cornerTile;
                        int2 sIndex = To2D(s, fieldColAmount);
                        int2 eIndex = To2D(e, fieldColAmount);
                        int2 divergent = distanceDifference.x > 0 ? sIndex : eIndex;
                        float2 loscPosition = cornerPos + new float2(tileSize / 2, -tileSize / 2);
                        float2 sourcePosition = waveFrontTilePos + new float2(-tileSize / 2, tileSize / 2);
                        NativeList<int2> blockOffsets = GetOffsets(sourcePosition, loscPosition, out int2 stepAmount);
                        int2 step = divergent;
                        SetLOSBlocks(blockOffsets, step, stepAmount);
                    }
                }
                if (isCornerFromSW)
                {
                    int2 swIndex2 = To2D(sw, fieldColAmount);
                    int2 swDistance = new int2(source.x - swIndex2.x, source.y - swIndex2.y);
                    int2 absSwDistance = new int2(math.abs(swDistance.x), math.abs(swDistance.y));
                    int2 distanceDifference = new int2(absCornerDistance.x - absSwDistance.x, absCornerDistance.y - absSwDistance.y);
                    if (distanceDifference.y * distanceDifference.x < 0) //if losc
                    {
                        UnsafeList<IntegrationTile> integrationSector = integrationField[sectorMarks[sectorIndex1d]].integrationSector;
                        IntegrationTile cornerTile = integrationSector[localIndex1d];
                        cornerTile.Mark = IntegrationMark.LOSC;
                        integrationSector[localIndex1d] = cornerTile;
                        int2 sIndex = To2D(s, fieldColAmount);
                        int2 wIndex = To2D(w, fieldColAmount);
                        int2 divergent = distanceDifference.x > 0 ? sIndex : wIndex;
                        float2 loscPosition = cornerPos + new float2(-tileSize / 2, -tileSize / 2);
                        float2 sourcePosition = waveFrontTilePos + new float2(tileSize / 2, tileSize / 2);
                        NativeList<int2> blockOffsets = GetOffsets(sourcePosition, loscPosition, out int2 stepAmount);
                        int2 step = divergent;
                        SetLOSBlocks(blockOffsets, step, stepAmount);
                    }
                }
                if (isCornerFromNW)
                {
                    int2 nwIndex2 = To2D(nw, fieldColAmount);
                    int2 nwDistance = new int2(source.x - nwIndex2.x, source.y - nwIndex2.y);
                    int2 absNwDistance = new int2(math.abs(nwDistance.x), math.abs(nwDistance.y));
                    int2 distanceDifference = new int2(absCornerDistance.x - absNwDistance.x, absCornerDistance.y - absNwDistance.y);
                    if (distanceDifference.y * distanceDifference.x < 0) //if losc
                    {
                        UnsafeList<IntegrationTile> integrationSector = integrationField[sectorMarks[sectorIndex1d]].integrationSector;
                        IntegrationTile cornerTile = integrationSector[localIndex1d];
                        cornerTile.Mark = IntegrationMark.LOSC;
                        integrationSector[localIndex1d] = cornerTile;
                        int2 nIndex = To2D(n, fieldColAmount);
                        int2 wIndex = To2D(w, fieldColAmount);
                        int2 divergent = distanceDifference.x > 0 ? nIndex : wIndex;
                        float2 loscPosition = cornerPos + new float2(-tileSize / 2, tileSize / 2);
                        float2 sourcePosition = waveFrontTilePos + new float2(tileSize / 2, -tileSize / 2);
                        NativeList<int2> blockOffsets = GetOffsets(sourcePosition, loscPosition, out int2 stepAmount);
                        int2 step = divergent;
                        SetLOSBlocks(blockOffsets, step, stepAmount);
                    }
                }

                //HELPERS
                bool IsCorner()
                {
                    return isCornerFromNE || isCornerFromSE || isCornerFromNW || isCornerFromSW;
                }
                NativeList<int2> GetOffsets(float2 start, float2 end, out int2 stepAmount)
                {
                    float2 p1 = start;
                    float2 p2 = end;
                    bool isYDecreasing = false;
                    bool isXDecreasing = false;
                    if (p2.x < p1.x)
                    {
                        isXDecreasing = true;
                        float dif = p1.x - p2.x;
                        p2.x = p2.x + dif * 2;
                    }
                    if (p2.y < p1.y)
                    {
                        isYDecreasing = true;
                        float dif = p1.y - p2.y;
                        p2.y = p2.y + dif * 2;
                    }
                    float2 p1Local = float2.zero;
                    float2 p2Local = p2 - p1;
                    float m = p2Local.y / p2Local.x;
                    if (m == float.PositiveInfinity)
                    {
                        NativeList<int2> infinityIndex = new NativeList<int2>(Allocator.Temp);
                        infinityIndex.Add(int2.zero);
                        stepAmount = isYDecreasing ? new int2(0, -1) : new int2(0, 1);
                        return infinityIndex;
                    }
                    NativeList<float2> points = GetPoints();
                    NativeList<int2> indicies = GetIndicies();
                    stepAmount = new int2((int)p2Local.x, (int)p2Local.y);
                    if (isYDecreasing) { stepAmount.y *= -1; }
                    if (isXDecreasing) { stepAmount.x *= -1; }
                    return indicies;

                    //HELPERS
                    NativeList<float2> GetPoints()
                    {
                        NativeList<float2> points = new NativeList<float2>(Allocator.Temp);
                        for (int i = 0; i <= p2Local.x; i++)
                        {
                            float y = m * i;
                            points.Add(new float2(i, y));
                        }
                        return points;
                    }
                    NativeList<int2> GetIndicies()
                    {
                        NativeList<int2> indicies = new NativeList<int2>(Allocator.Temp);
                        for (int i = 0; i < points.Length - 1; i++)
                        {
                            float2 next = points[i + 1];
                            float2 cur = points[i];
                            int curx = (int)cur.x;
                            int cury = (int)cur.y;
                            int nexty = (int)(next.y - 0.000001f);
                            for (int j = cury; j <= nexty; j++)
                            {
                                int2 index = new int2(curx, j);
                                if (isYDecreasing)
                                {
                                    index.y *= -1;
                                }
                                if (isXDecreasing)
                                {
                                    index.x *= -1;
                                }
                                indicies.Add(index);
                            }
                        }
                        return indicies;
                    }
                }
                void SetLOSBlocks(NativeList<int2> blockOffsets, int2 step, int2 stepAmount)
                {
                    bool stopCalculating = false;
                    while (!IsOutOfBounds2D(step) && !stopCalculating)
                    {
                        for (int i = 0; i < blockOffsets.Length; i++)
                        {
                            int2 resultingIndex2d = blockOffsets[i] + step;
                            if (IsOutOfBounds2D(resultingIndex2d)) { break; }
                            int resultingIndex1d = To1D(resultingIndex2d, fieldColAmount);
                            int2 resultingSectorIndex2d = GetSectorIndex(resultingIndex2d);
                            int resultingSectorIndex1d = To1D(resultingSectorIndex2d, sectorMatrixColAmount);
                            if (sectorMarks[resultingSectorIndex1d] == 0) { stopCalculating = true; break; }
                            int2 resultingLocalIndex2d = GetLocalIndex(resultingIndex2d, new int2(resultingSectorIndex2d.x * sectorTileAmount, resultingSectorIndex2d.y * sectorTileAmount));
                            int resultingLocalIndex1d = To1D(resultingLocalIndex2d, sectorTileAmount);
                            UnsafeList<IntegrationTile> resultingIndexSector = integrationField[sectorMarks[resultingSectorIndex1d]].integrationSector;
                            IntegrationTile tile = resultingIndexSector[resultingLocalIndex1d];
                            if (tile.Mark == IntegrationMark.LOSBlock) { continue; }
                            else if (costs[resultingIndex1d] == byte.MaxValue) { stopCalculating = true; break; }
                            tile.Mark = IntegrationMark.LOSBlock;
                            resultingIndexSector[resultingLocalIndex1d] = tile;
                            blockedWaveFronts.Enqueue(new LocalIndex1d(resultingLocalIndex1d, resultingSectorIndex1d));
                        }
                        step += stepAmount;
                    }
                }
            }
        }
        int To1D(int2 index2, int colAmount)
        {
            return index2.y * colAmount + index2.x;
        }
        int2 To2D(int index, int colAmount)
        {
            return new int2(index % colAmount, index / colAmount);
        }
        int2 GetSectorIndex(int2 index)
        {
            return new int2(index.x / sectorTileAmount, index.y / sectorTileAmount);
        }
        int2 GetLocalIndex(int2 index, int2 sectorStartIndex)
        {
            return index - sectorStartIndex;
        }
        int2 GetSectorStartIndex(int2 sectorIndex)
        {
            return new int2(sectorIndex.x * sectorTileAmount, sectorIndex.y * sectorTileAmount);
        }
        int GetGeneral1d(int2 local2d, int2 sector2d)
        {
            int2 sectorStart = GetSectorStartIndex(sector2d);
            int2 general2d = local2d + sectorStart;
            int general1d = To1D(general2d, fieldColAmount);
            return general1d;
        }
        bool IsOutOfBounds1D(int index)
        {
            int2 i2d = To2D(index, fieldColAmount);
            if (i2d.x >= fieldColAmount) { return true; }
            if (i2d.y >= fieldRowAmount) { return true; }
            if (i2d.x <= 0) { return true; }
            if (i2d.y <= 0) { return true; }
            return false;
        }
        bool IsOutOfBounds2D(int2 index)
        {
            if (index.x >= fieldColAmount) { return true; }
            if (index.y >= fieldRowAmount) { return true; }
            if (index.x <= 0) { return true; }
            if (index.y <= 0) { return true; }
            return false;
        }
    }
}