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
public struct LOSJobRefactored : IJob
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
    public NativeList<UnsafeList<IntegrationTile>> IntegrationField;
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
        NativeList<UnsafeList<IntegrationTile>> integrationField = IntegrationField;
        NativeQueue<LocalIndex2d> integrationQueue = IntegrationQueue;
        NativeArray<byte> costs = Costs;
        NativeQueue<LocalIndex1d> blockedWaveFronts = BlockedWaveFronts;

        ///////CURRENT INDEX LOOKUP TABLE////////
        /////////////////////////////////////////
        int curLocal1d = 0;
        int curSector1d = 0;
        int2 nLocal2d = new int2();
        int2 eLocal2d = new int2();
        int2 sLocal2d = new int2();
        int2 wLocal2d = new int2();
        int2 nSector2d = new int2();
        int2 eSector2d = new int2();
        int2 sSector2d = new int2();
        int2 wSector2d = new int2();
        int nLocal1d = 0;
        int eLocal1d = 0;
        int sLocal1d = 0;
        int wLocal1d = 0;
        int nGeneral1d = 0;
        int eGeneral1d = 0;
        int sGeneral1d = 0;
        int wGeneral1d = 0;
        int nSector1d = 0;
        int eSector1d = 0;
        int sSector1d = 0;
        int wSector1d = 0;
        byte nCost = 0;
        byte eCost = 0;
        byte sCost = 0;
        byte wCost = 0;
        bool nRelevant = false;
        bool eRelevant = false;
        bool sRelevant = false;
        bool wRelevant = false;
        ///////////////////////////////////////////

        //SET TARGET INDEX
        int2 targetSector2d = GetSectorIndex(Target);
        int targetSector1d = To1D(targetSector2d, SectorMatrixColAmount);
        int2 targetSectorStartIndex = new int2(targetSector2d.x * SectorTileAmount, targetSector2d.y * SectorTileAmount);
        int2 targetLocalIndex2d = GetLocalIndex(Target, targetSectorStartIndex);
        int targetLocalIndex1d = To1D(targetLocalIndex2d, SectorTileAmount);
        UnsafeList<IntegrationTile> targetSector = IntegrationField[SectorMarks[targetSector1d]];
        IntegrationTile targetTile = targetSector[targetLocalIndex1d];
        targetTile.Cost = 0f;
        targetTile.Mark = IntegrationMark.LOSPass;
        targetSector[targetLocalIndex1d] = targetTile;
        IntegrationField[SectorMarks[targetSector1d]] = targetSector;
        SetLookupTable(targetLocalIndex2d, targetSector2d);
        DetermineLOSC();
        EnqueueNeighbours(1f);
        while (!integrationQueue.IsEmpty())
        {
            LocalIndex2d curIndex = integrationQueue.Dequeue();
            SetLookupTable(curIndex.index, curIndex.sector);
            float curCost = integrationField[SectorMarks[curSector1d]][curLocal1d].Cost;
            UnsafeList<IntegrationTile> integrationSector = integrationField[SectorMarks[curSector1d]];
            IntegrationTile curTile = integrationSector[curLocal1d];
            if(curTile.Mark == IntegrationMark.LOSBlock) { continue; }
            curTile.Mark = IntegrationMark.LOSPass;
            DetermineLOSC();
            EnqueueNeighbours(curCost + 1f);
        }
        void SetLookupTable(int2 curLocal2d, int2 curSector2d)
        {

            //data
            nLocal2d = new int2(curLocal2d.x, curLocal2d.y + 1);
            eLocal2d = new int2(curLocal2d.x + 1, curLocal2d.y);
            sLocal2d = new int2(curLocal2d.x, curLocal2d.y - 1);
            wLocal2d = new int2(curLocal2d.x - 1, curLocal2d.y);
            nSector2d = curSector2d;
            eSector2d = curSector2d;
            sSector2d = curSector2d;
            wSector2d = curSector2d;

            //calculation
            bool nOverflow = nLocal2d.y >= sectorTileAmount;
            bool eOverflow = eLocal2d.x >= sectorTileAmount;
            bool sOverflow = sLocal2d.y < 0;
            bool wOverflow = wLocal2d.x < 0;

            curSector1d = To1D(curSector2d, sectorMatrixColAmount);
            nLocal2d = math.select(nLocal2d, new int2(nLocal2d.x, 0), nOverflow);
            eLocal2d = math.select(eLocal2d, new int2(0, eLocal2d.y), eOverflow);
            sLocal2d = math.select(sLocal2d, new int2(sLocal2d.x, sectorTileAmount - 1), sOverflow);
            wLocal2d = math.select(wLocal2d, new int2(sectorTileAmount - 1, wLocal2d.y), wOverflow);

            nSector2d = math.select(nSector2d, new int2(nSector2d.x, nSector2d.y + 1), nOverflow);
            eSector2d = math.select(eSector2d, new int2(eSector2d.x + 1, eSector2d.y), eOverflow);
            sSector2d = math.select(sSector2d, new int2(sSector2d.x, sSector2d.y - 1), sOverflow);
            wSector2d = math.select(wSector2d, new int2(wSector2d.x - 1, wSector2d.y), wOverflow);

            curLocal1d = To1D(curLocal2d, sectorTileAmount);
            nLocal1d = To1D(nLocal2d, sectorTileAmount);
            eLocal1d = To1D(eLocal2d, sectorTileAmount);
            sLocal1d = To1D(sLocal2d, sectorTileAmount);
            wLocal1d = To1D(wLocal2d, sectorTileAmount);
            nSector1d = To1D(nSector2d, sectorMatrixColAmount);
            eSector1d = To1D(eSector2d, sectorMatrixColAmount);
            sSector1d = To1D(sSector2d, sectorMatrixColAmount);
            wSector1d = To1D(wSector2d, sectorMatrixColAmount);

            nGeneral1d = To1D(new int2(nSector2d.x * sectorTileAmount, nSector2d.y * sectorTileAmount) + nLocal2d, fieldColAmount);
            eGeneral1d = To1D(new int2(eSector2d.x * sectorTileAmount, eSector2d.y * sectorTileAmount) + eLocal2d, fieldColAmount);
            sGeneral1d = To1D(new int2(sSector2d.x * sectorTileAmount, sSector2d.y * sectorTileAmount) + sLocal2d, fieldColAmount);
            wGeneral1d = To1D(new int2(wSector2d.x * sectorTileAmount, wSector2d.y * sectorTileAmount) + wLocal2d, fieldColAmount);

            int nSectorIndex = sectorMarks[nSector1d];
            int eSectorIndex = sectorMarks[eSector1d];
            int sSectorIndex = sectorMarks[sSector1d];
            int wSectorIndex = sectorMarks[wSector1d];
            nRelevant = integrationField[nSectorIndex].Length != 0 && integrationField[nSectorIndex][nLocal1d].Mark == IntegrationMark.None;
            eRelevant = integrationField[eSectorIndex].Length != 0 && integrationField[eSectorIndex][eLocal1d].Mark == IntegrationMark.None;
            sRelevant = integrationField[sSectorIndex].Length != 0 && integrationField[sSectorIndex][sLocal1d].Mark == IntegrationMark.None;
            wRelevant = integrationField[wSectorIndex].Length != 0 && integrationField[wSectorIndex][wLocal1d].Mark == IntegrationMark.None;

            nCost = costs[nGeneral1d];
            eCost = costs[eGeneral1d];
            sCost = costs[sGeneral1d];
            wCost = costs[wGeneral1d];
        }
        void EnqueueNeighbours(float newWaveCost)
        {
            UnsafeList<IntegrationTile> nSector = integrationField[sectorMarks[nSector1d]];
            UnsafeList<IntegrationTile> eSector = integrationField[sectorMarks[eSector1d]];
            UnsafeList<IntegrationTile> sSector = integrationField[sectorMarks[sSector1d]];
            UnsafeList<IntegrationTile> wSector = integrationField[sectorMarks[wSector1d]];
            if (nRelevant && nCost != byte.MaxValue)
            {
                integrationQueue.Enqueue(new LocalIndex2d(nLocal2d, nSector2d));
                IntegrationTile tile = nSector[nLocal1d];
                tile.Mark = IntegrationMark.Awaiting;
                tile.Cost = newWaveCost;
                nSector[nLocal1d] = tile;

            }
            if (eRelevant && eCost != byte.MaxValue)
            {
                integrationQueue.Enqueue(new LocalIndex2d(eLocal2d, eSector2d));
                IntegrationTile tile = eSector[eLocal1d];
                tile.Mark = IntegrationMark.Awaiting;
                tile.Cost = newWaveCost;
                eSector[eLocal1d] = tile;
            }
            if (sRelevant && sCost != byte.MaxValue)
            {
                integrationQueue.Enqueue(new LocalIndex2d(sLocal2d, sSector2d));
                IntegrationTile tile = sSector[sLocal1d];
                tile.Mark = IntegrationMark.Awaiting;
                tile.Cost = newWaveCost;
                sSector[sLocal1d] = tile;
            }
            if (wRelevant && wCost != byte.MaxValue)
            {
                integrationQueue.Enqueue(new LocalIndex2d(wLocal2d, wSector2d));
                IntegrationTile tile = wSector[wLocal1d];
                tile.Mark = IntegrationMark.Awaiting;
                tile.Cost = newWaveCost;
                wSector[wLocal1d] = tile;
            }
        }
        void DetermineLOSC()
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
                    CalculateForNE();
                }
                if (isCornerFromSE)
                {
                    CalculateForSE();
                }
                if (isCornerFromSW)
                {
                    CalculateForSW();
                }
                if (isCornerFromNW)
                {
                    CalculateForNW();
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
                            UnsafeList<IntegrationTile> resultingIndexSector = integrationField[sectorMarks[resultingSectorIndex1d]];
                            IntegrationTile tile = resultingIndexSector[resultingLocalIndex1d];
                            if (tile.Mark == IntegrationMark.LOSBlock) { continue; }
                            else if (costs[resultingIndex1d] == byte.MaxValue) { continue; }
                            tile.Mark = IntegrationMark.LOSBlock;
                            resultingIndexSector[resultingLocalIndex1d] = tile;
                            blockedWaveFronts.Enqueue(new LocalIndex1d(resultingIndex1d, resultingSectorIndex1d));
                        }
                        step += stepAmount;
                    }
                }
                void CalculateForNE()
                {
                    int2 neIndex2 = To2D(ne, fieldColAmount);
                    int2 neDistance = new int2(source.x - neIndex2.x, source.y - neIndex2.y);
                    int2 absNeDistance = new int2(math.abs(neDistance.x), math.abs(neDistance.y));
                    int2 distanceDifference = new int2(absCornerDistance.x - absNeDistance.x, absCornerDistance.y - absNeDistance.y);
                    if (distanceDifference.y * distanceDifference.x < 0) //if losc
                    {
                        UnsafeList<IntegrationTile> integrationSector = integrationField[sectorMarks[sectorIndex1d]];
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
                void CalculateForSE()
                {
                    int2 seIndex2 = To2D(se, fieldColAmount);
                    int2 seDistance = new int2(source.x - seIndex2.x, source.y - seIndex2.y);
                    int2 absSeDistance = new int2(math.abs(seDistance.x), math.abs(seDistance.y));
                    int2 distanceDifference = new int2(absCornerDistance.x - absSeDistance.x, absCornerDistance.y - absSeDistance.y);
                    if (distanceDifference.y * distanceDifference.x < 0) //if losc
                    {
                        UnsafeList<IntegrationTile> integrationSector = integrationField[sectorMarks[sectorIndex1d]];
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
                void CalculateForSW()
                {
                    int2 swIndex2 = To2D(sw, fieldColAmount);
                    int2 swDistance = new int2(source.x - swIndex2.x, source.y - swIndex2.y);
                    int2 absSwDistance = new int2(math.abs(swDistance.x), math.abs(swDistance.y));
                    int2 distanceDifference = new int2(absCornerDistance.x - absSwDistance.x, absCornerDistance.y - absSwDistance.y);
                    if (distanceDifference.y * distanceDifference.x < 0) //if losc
                    {
                        UnsafeList<IntegrationTile> integrationSector = integrationField[sectorMarks[sectorIndex1d]];
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
                void CalculateForNW()
                {
                    int2 nwIndex2 = To2D(nw, fieldColAmount);
                    int2 nwDistance = new int2(source.x - nwIndex2.x, source.y - nwIndex2.y);
                    int2 absNwDistance = new int2(math.abs(nwDistance.x), math.abs(nwDistance.y));
                    int2 distanceDifference = new int2(absCornerDistance.x - absNwDistance.x, absCornerDistance.y - absNwDistance.y);
                    if (distanceDifference.y * distanceDifference.x < 0) //if losc
                    {
                        UnsafeList<IntegrationTile> integrationSector = integrationField[sectorMarks[sectorIndex1d]];
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
public struct LocalIndex2d
{
    public int2 index;
    public int2 sector;

    public LocalIndex2d(int2 localIndex, int2 sectorIndex)
    {
        index = localIndex;
        sector = sectorIndex;
    }
}