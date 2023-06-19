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
    public NativeArray<int> SectorMarks;
    public NativeList<IntegrationFieldSector> IntegrationField;
    public NativeQueue<LocalIndex2d> IntegrationQueue;
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
        NativeQueue<LocalIndex2d> integrationQueue = IntegrationQueue;
        NativeArray<byte> costs = Costs;
        NativeQueue<LocalIndex1d> blockedWaveFronts = BlockedWaveFronts;

        ///////CURRENT INDEX LOOKUP TABLE////////
        /////////////////////////////////////////
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
        int nLocal1d;
        int eLocal1d;
        int sLocal1d;
        int wLocal1d;
        int nGeneral1d;
        int eGeneral1d;
        int sGeneral1d;
        int wGeneral1d;
        int nSector1d;
        int eSector1d;
        int sSector1d;
        int wSector1d;
        byte nCost;
        byte eCost;
        byte sCost;
        byte wCost;
        bool nRelevant;
        bool eRelevant;
        bool sRelevant;
        bool wRelevant;
        UnsafeList<IntegrationTile> nSector;
        UnsafeList<IntegrationTile> eSector;
        UnsafeList<IntegrationTile> sSector;
        UnsafeList<IntegrationTile> wSector;
        ///////////////////////////////////////////

        //SET TARGET INDEX
        int2 targetSector2d = GetSectorIndex(Target);
        int targetSector1d = To1D(targetSector2d, SectorMatrixColAmount);
        int2 targetSectorStartIndex = new int2(targetSector2d.x * SectorTileAmount, targetSector2d.y * SectorTileAmount);
        int2 targetLocal2d = GetLocalIndex(Target, targetSectorStartIndex);
        int targetLocal1d = To1D(targetLocal2d, SectorTileAmount);

        UnsafeList<IntegrationTile> targetSector = IntegrationField[SectorMarks[targetSector1d]].integrationSector;
        IntegrationTile targetTile = targetSector[targetLocal1d];
        targetTile.Cost = 0f;
        targetTile.Mark = IntegrationMark.LOSPass;
        targetSector[targetLocal1d] = targetTile;
        SetLookupTable(targetLocal2d, targetSector2d);
        LookForLOSC();
        EnqueueNeighbours(1f);
        while (!integrationQueue.IsEmpty())
        {
            LocalIndex2d curIndex = integrationQueue.Dequeue();
            SetLookupTable(curIndex.index, curIndex.sector);
            int curSectorMark = SectorMarks[curSector1d];
            float curCost = integrationField[curSectorMark].integrationSector[curLocal1d].Cost;
            UnsafeList<IntegrationTile> integrationSector = integrationField[curSectorMark].integrationSector;
            IntegrationTile curTile = integrationSector[curLocal1d];
            if(curTile.Mark == IntegrationMark.LOSBlock) { continue; }
            curTile.Mark = IntegrationMark.LOSPass;
            integrationSector[curLocal1d] = curTile;
            LookForLOSC();
            EnqueueNeighbours(curCost + 1f);
        }
        void SetLookupTable(int2 curLocal2d, int2 curSector2d)
        {
            //LOCAL 2D INDICIES
            nLocal2d = new int2(curLocal2d.x, curLocal2d.y + 1);
            eLocal2d = new int2(curLocal2d.x + 1, curLocal2d.y);
            sLocal2d = new int2(curLocal2d.x, curLocal2d.y - 1);
            wLocal2d = new int2(curLocal2d.x - 1, curLocal2d.y);

            //SECTOR 2D INDICIES
            nSector2d = curSector2d;
            eSector2d = curSector2d;
            sSector2d = curSector2d;
            wSector2d = curSector2d;

            //LOCAL OVERFLOWS
            bool nOverflow = nLocal2d.y >= sectorTileAmount;
            bool eOverflow = eLocal2d.x >= sectorTileAmount;
            bool sOverflow = sLocal2d.y < 0;
            bool wOverflow = wLocal2d.x < 0;

            //HANDLE OVERFLOWS
            nLocal2d = math.select(nLocal2d, new int2(nLocal2d.x, 0), nOverflow);
            eLocal2d = math.select(eLocal2d, new int2(0, eLocal2d.y), eOverflow);
            sLocal2d = math.select(sLocal2d, new int2(sLocal2d.x, sectorTileAmount - 1), sOverflow);
            wLocal2d = math.select(wLocal2d, new int2(sectorTileAmount - 1, wLocal2d.y), wOverflow);
            nSector2d = math.select(nSector2d, new int2(nSector2d.x, nSector2d.y + 1), nOverflow);
            eSector2d = math.select(eSector2d, new int2(eSector2d.x + 1, eSector2d.y), eOverflow);
            sSector2d = math.select(sSector2d, new int2(sSector2d.x, sSector2d.y - 1), sOverflow);
            wSector2d = math.select(wSector2d, new int2(wSector2d.x - 1, wSector2d.y), wOverflow);

            //LOCAL 1D INDICIES
            curLocal1d = To1D(curLocal2d, sectorTileAmount);
            nLocal1d = To1D(nLocal2d, sectorTileAmount);
            eLocal1d = To1D(eLocal2d, sectorTileAmount);
            sLocal1d = To1D(sLocal2d, sectorTileAmount);
            wLocal1d = To1D(wLocal2d, sectorTileAmount);
            nSector1d = To1D(nSector2d, sectorMatrixColAmount);
            eSector1d = To1D(eSector2d, sectorMatrixColAmount);
            sSector1d = To1D(sSector2d, sectorMatrixColAmount);
            wSector1d = To1D(wSector2d, sectorMatrixColAmount);

            //GENERAL 1D INDICIES
            nGeneral1d = To1D(new int2(nSector2d.x * sectorTileAmount, nSector2d.y * sectorTileAmount) + nLocal2d, fieldColAmount);
            eGeneral1d = To1D(new int2(eSector2d.x * sectorTileAmount, eSector2d.y * sectorTileAmount) + eLocal2d, fieldColAmount);
            sGeneral1d = To1D(new int2(sSector2d.x * sectorTileAmount, sSector2d.y * sectorTileAmount) + sLocal2d, fieldColAmount);
            wGeneral1d = To1D(new int2(wSector2d.x * sectorTileAmount, wSector2d.y * sectorTileAmount) + wLocal2d, fieldColAmount);

            //SECTOR MARKS
            int nSectorMark = sectorMarks[nSector1d];
            int eSectorMark = sectorMarks[eSector1d];
            int sSectorMark = sectorMarks[sSector1d];
            int wSectorMark = sectorMarks[wSector1d];

            //SECTORS
            nSector = integrationField[nSectorMark].integrationSector;
            eSector = integrationField[eSectorMark].integrationSector;
            sSector = integrationField[sSectorMark].integrationSector;
            wSector = integrationField[wSectorMark].integrationSector;

            //RELEVANCIES
            nRelevant = nSectorMark != 0 && integrationField[nSectorMark].integrationSector[nLocal1d].Mark == IntegrationMark.None;
            eRelevant = eSectorMark != 0 && integrationField[eSectorMark].integrationSector[eLocal1d].Mark == IntegrationMark.None;
            sRelevant = sSectorMark != 0 && integrationField[sSectorMark].integrationSector[sLocal1d].Mark == IntegrationMark.None;
            wRelevant = wSectorMark != 0 && integrationField[wSectorMark].integrationSector[wLocal1d].Mark == IntegrationMark.None;

            //COSTS
            nCost = costs[nGeneral1d];
            eCost = costs[eGeneral1d];
            sCost = costs[sGeneral1d];
            wCost = costs[wGeneral1d];

            curSector1d = To1D(curSector2d, sectorMatrixColAmount);
        }
        void EnqueueNeighbours(float newWaveCost)
        {
            bool nEnqueueable = nRelevant && nCost != byte.MaxValue;
            bool eEnqueueable = eRelevant && eCost != byte.MaxValue;
            bool sEnqueueable = sRelevant && sCost != byte.MaxValue;
            bool wEnqueueable = wRelevant && wCost != byte.MaxValue;
            if (nEnqueueable)
            {
                integrationQueue.Enqueue(new LocalIndex2d(nLocal2d, nSector2d));
                IntegrationTile tile = nSector[nLocal1d];
                tile.Mark = IntegrationMark.Awaiting;
                tile.Cost = newWaveCost;
                nSector[nLocal1d] = tile;

            }
            if (eEnqueueable)
            {
                integrationQueue.Enqueue(new LocalIndex2d(eLocal2d, eSector2d));
                IntegrationTile tile = eSector[eLocal1d];
                tile.Mark = IntegrationMark.Awaiting;
                tile.Cost = newWaveCost;
                eSector[eLocal1d] = tile;
            }
            if (sEnqueueable)
            {
                integrationQueue.Enqueue(new LocalIndex2d(sLocal2d, sSector2d));
                IntegrationTile tile = sSector[sLocal1d];
                tile.Mark = IntegrationMark.Awaiting;
                tile.Cost = newWaveCost;
                sSector[sLocal1d] = tile;
            }
            if (wEnqueueable)
            {
                integrationQueue.Enqueue(new LocalIndex2d(wLocal2d, wSector2d));
                IntegrationTile tile = wSector[wLocal1d];
                tile.Mark = IntegrationMark.Awaiting;
                tile.Cost = newWaveCost;
                wSector[wLocal1d] = tile;
            }
        }
        void LookForLOSC()
        {
            if (nRelevant && nCost == byte.MaxValue)
            {
                ApplyLOSBlockIfLOSCorner(nGeneral1d, nLocal1d, nSector1d);
            }
            if (eRelevant && eCost == byte.MaxValue)
            {
                ApplyLOSBlockIfLOSCorner(eGeneral1d, eLocal1d, eSector1d);
            }
            if (sRelevant && sCost == byte.MaxValue)
            {
                ApplyLOSBlockIfLOSCorner(sGeneral1d, sLocal1d, sSector1d);
            }
            if (wRelevant && wCost == byte.MaxValue)
            {
                ApplyLOSBlockIfLOSCorner(wGeneral1d, wLocal1d, wSector1d);
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
                            else if (costs[resultingIndex1d] == byte.MaxValue) { continue; }
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