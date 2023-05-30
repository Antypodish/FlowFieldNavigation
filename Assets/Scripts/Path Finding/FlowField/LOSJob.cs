using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
[BurstCompile]
public struct LOSJob : IJob
{
    public int InitialWaveFront;
    public float TileSize;
    public int FieldColAmount;
    public int FieldRowAmount;
    public NativeArray<IntegrationTile> IntegrationField;
    [ReadOnly] public NativeArray<byte> Costs;
    [ReadOnly] public NativeArray<DirectionData> Directions;
    [WriteOnly] public NativeQueue<int> BlockedWaveFronts;
    public void Execute()
    {
        NativeQueue<int> integrationQueue = new NativeQueue<int>(Allocator.Temp);
        LOSIntegration(integrationQueue);
    }
    void LOSIntegration(NativeQueue<int> integrationQueue)
    {
        //DATA
        float tileSize = TileSize;
        int initialWaveFront = InitialWaveFront;
        int fieldColAmount = FieldColAmount;
        int fieldRowAmount = FieldRowAmount;
        NativeArray<IntegrationTile> integrationField = IntegrationField;
        NativeArray<DirectionData> directionData = Directions;
        NativeArray<byte> costs = Costs;
        NativeQueue<int> blockedWaveFronts = BlockedWaveFronts;

        //NEIGHBOUR LOOKUP TABLE
        int n = 0;
        int e = 0;
        int s = 0;
        int w = 0;
        IntegrationMark nMark = IntegrationMark.Irrelevant;
        IntegrationMark eMark = IntegrationMark.Irrelevant;
        IntegrationMark sMark = IntegrationMark.Irrelevant;
        IntegrationMark wMark = IntegrationMark.Irrelevant;
        bool northIsRelevant = false;
        bool eastIsRelevant = false;
        bool southIsRelevant = false;
        bool westIsRelevant = false;
        byte nCost = 0;
        byte eCost = 0;
        byte sCost = 0;
        byte wCost = 0;

        //ALGORITHM (ORDER IS IMPORTANT)
        int targetIndex = InitialWaveFront;
        DirectionData targetDirections = Directions[targetIndex];
        SetNeighbourLookupTable(targetDirections);
        DetermineLOSC();
        IntegrationTile targetTile = IntegrationField[targetIndex];
        if (targetTile.Mark != IntegrationMark.LOSBlock)
        {
            targetTile.Mark = IntegrationMark.LOSPass;
            targetTile.Cost = 0f;
            IntegrationField[targetIndex] = targetTile;
            EnqueueDirections(1f);
        }
        while (!integrationQueue.IsEmpty())
        {
            int index = integrationQueue.Dequeue();
            float cost = integrationField[index].Cost + 1f;
            DirectionData indexDirections = Directions[index];
            SetNeighbourLookupTable(indexDirections);
            DetermineLOSC();
            IntegrationTile tile = IntegrationField[index];
            if (tile.Mark == IntegrationMark.LOSBlock) { continue; }
            else
            {
                tile.Cost = cost;
                tile.Mark = IntegrationMark.LOSPass;
                integrationField[index] = tile;
                EnqueueDirections(cost);
            }
        }

        //HELPERS
        void SetNeighbourLookupTable(DirectionData directions)
        {
            n = directions.N;
            e = directions.E;
            s = directions.S;
            w = directions.W;
            nMark = integrationField[n].Mark;
            eMark = integrationField[e].Mark;
            sMark = integrationField[s].Mark;
            wMark = integrationField[w].Mark;
            northIsRelevant = nMark == IntegrationMark.Relevant;
            eastIsRelevant = eMark == IntegrationMark.Relevant;
            southIsRelevant = sMark == IntegrationMark.Relevant;
            westIsRelevant = wMark == IntegrationMark.Relevant;
            nCost = costs[n];
            eCost = costs[e];
            sCost = costs[s];
            wCost = costs[w];
        }
        void EnqueueDirections(float waveCost)
        {
            if (northIsRelevant && nCost != byte.MaxValue)
            {
                IntegrationTile tile = integrationField[n];
                integrationQueue.Enqueue(n);
                tile.Mark = IntegrationMark.Awaiting;
                tile.Cost = waveCost;
                integrationField[n] = tile;

            }
            if (eastIsRelevant && eCost != byte.MaxValue)
            {
                IntegrationTile tile = integrationField[e];
                integrationQueue.Enqueue(e);
                tile.Mark = IntegrationMark.Awaiting;
                tile.Cost = waveCost;
                integrationField[e] = tile;
            }
            if (southIsRelevant && sCost != byte.MaxValue)
            {
                IntegrationTile tile = integrationField[s];
                integrationQueue.Enqueue(s);
                tile.Mark = IntegrationMark.Awaiting;
                tile.Cost = waveCost;
                integrationField[s] = tile;
            }
            if (westIsRelevant && wCost != byte.MaxValue)
            {
                IntegrationTile tile = integrationField[w];
                integrationQueue.Enqueue(w);
                tile.Mark = IntegrationMark.Awaiting;
                tile.Cost = waveCost;
                integrationField[w] = tile;
            }
        }
        void DetermineLOSC()
        {
            if (northIsRelevant && nCost == byte.MaxValue)
            {
                ApplyLOSBlockIfLOSCorner(n);
            }
            if (eastIsRelevant && eCost == byte.MaxValue)
            {
                ApplyLOSBlockIfLOSCorner(e);
            }
            if (southIsRelevant && sCost == byte.MaxValue)
            {
                ApplyLOSBlockIfLOSCorner(s);
            }
            if (westIsRelevant && wCost == byte.MaxValue)
            {
                ApplyLOSBlockIfLOSCorner(w);
            }

            void ApplyLOSBlockIfLOSCorner(int index)
            {
                //NEIGHBOUR LOOKUP TABLE
                int n = directionData[index].N;
                int e = directionData[index].E;
                int s = directionData[index].S;
                int w = directionData[index].W;
                int ne = directionData[index].NE;
                int se = directionData[index].SE;
                int sw = directionData[index].SW;
                int nw = directionData[index].NW;
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
                int2 source = To2D(initialWaveFront, fieldColAmount);
                int2 cornerIndex = To2D(index, fieldColAmount);
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
                            int resultingIndex1d = To1D(resultingIndex2d, fieldColAmount);
                            IntegrationTile tile = integrationField[resultingIndex1d];
                            if (IsOutOfBounds2D(resultingIndex2d)) { break; }
                            else if (tile.Mark == IntegrationMark.Irrelevant) { stopCalculating = true; break; }
                            else if (costs[resultingIndex1d] == byte.MaxValue) { stopCalculating = true; break; }
                            else if (tile.Mark == IntegrationMark.LOSBlock) { stopCalculating = true; break; }
                            tile.Mark = IntegrationMark.LOSBlock;
                            integrationField[resultingIndex1d] = tile;
                            blockedWaveFronts.Enqueue(resultingIndex1d);
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
                        IntegrationTile cornerTile = integrationField[index];
                        cornerTile.Mark = IntegrationMark.LOSC;
                        integrationField[index] = cornerTile;
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
                        IntegrationTile cornerTile = integrationField[index];
                        cornerTile.Mark = IntegrationMark.LOSC;
                        integrationField[index] = cornerTile;
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
                        IntegrationTile cornerTile = integrationField[index];
                        cornerTile.Mark = IntegrationMark.LOSC;
                        integrationField[index] = cornerTile;
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
                        IntegrationTile cornerTile = integrationField[index];
                        cornerTile.Mark = IntegrationMark.LOSC;
                        integrationField[index] = cornerTile;
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