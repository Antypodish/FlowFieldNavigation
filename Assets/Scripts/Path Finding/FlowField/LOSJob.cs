using System;
using System.Collections.Generic;
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
    public NativeList<int> CornerTiles;
    public NativeList<Vector2> CornerPositions;
    [ReadOnly] public NativeArray<byte> Costs;
    [ReadOnly] public NativeArray<DirectionData> Directions;
    public void Execute()
    {
        NativeQueue<LOSQueueElement> integrationQueue = new NativeQueue<LOSQueueElement>(Allocator.Temp);
        LOSIntegration(integrationQueue);
    }
    void LOSIntegration(NativeQueue<LOSQueueElement> integrationQueue)
    {
        float tileSize = TileSize;
        int initialWaveFront = InitialWaveFront;
        int fieldColAmount = FieldColAmount;
        int fieldRowAmount = FieldRowAmount;
        NativeArray<IntegrationTile> integrationField = IntegrationField;
        NativeArray<DirectionData> directions = Directions;
        NativeList<int> cornerTiles = CornerTiles;
        NativeList<Vector2> cornerPositions = CornerPositions;
        NativeArray<byte> costs = Costs;

        int targetLocalIndex = InitialWaveFront;
        DetermineLOSC(Directions[targetLocalIndex]);
        IntegrationTile targetTile = IntegrationField[targetLocalIndex];
        if (targetTile.Mark != IntegrationMark.LOSBlock)
        {
            targetTile.Mark = IntegrationMark.LOSPass;
            targetTile.Cost = 0f;
            IntegrationField[targetLocalIndex] = targetTile;
            EnqueueDirections(Directions[targetLocalIndex], 0f);
        }
        while (!integrationQueue.IsEmpty())
        {
            LOSQueueElement queueElement = integrationQueue.Dequeue();
            int index = queueElement.Index;
            float cost = queueElement.PreviousCost + 1;
            DetermineLOSC(Directions[index]);
            IntegrationTile tile = IntegrationField[index];
            if (tile.Mark == IntegrationMark.LOSBlock) { continue; }
            else
            {
                tile.Cost = cost;
                tile.Mark = IntegrationMark.LOSPass;
                integrationField[index] = tile;
                EnqueueDirections(Directions[index], cost);
            }
        }

        //HELPERS
        void EnqueueDirections(DirectionData directions, float waveCost)
        {
            int n = directions.N;
            int e = directions.E;
            int s = directions.S;
            int w = directions.W;
            //MARKS
            IntegrationMark nMark = integrationField[n].Mark;
            IntegrationMark eMark = integrationField[e].Mark;
            IntegrationMark sMark = integrationField[s].Mark;
            IntegrationMark wMark = integrationField[w].Mark;
            //RELEVANCIES
            bool northIsRelevant = nMark == IntegrationMark.Relevant;
            bool eastIsRelevant = eMark == IntegrationMark.Relevant;
            bool southIsRelevant = sMark == IntegrationMark.Relevant;
            bool westIsRelevant = wMark == IntegrationMark.Relevant;

            byte nCost = costs[n];
            byte eCost = costs[e];
            byte sCost = costs[s];
            byte wCost = costs[w];
            if (northIsRelevant && nCost != byte.MaxValue)
            {
                IntegrationTile tile = integrationField[n];
                LOSQueueElement queueElement = new LOSQueueElement()
                {
                    Index = n,
                    PreviousCost = waveCost,
                };
                integrationQueue.Enqueue(queueElement);
                tile.Mark = IntegrationMark.Awaiting;
                integrationField[n] = tile;

            }
            if (eastIsRelevant && eCost != byte.MaxValue)
            {
                IntegrationTile tile = integrationField[e];
                LOSQueueElement queueElement = new LOSQueueElement()
                {
                    Index = e,
                    PreviousCost = waveCost,
                };
                integrationQueue.Enqueue(queueElement);
                tile.Mark = IntegrationMark.Awaiting;
                integrationField[e] = tile;
            }
            if (southIsRelevant && sCost != byte.MaxValue)
            {
                IntegrationTile tile = integrationField[s];
                LOSQueueElement queueElement = new LOSQueueElement()
                {
                    Index = s,
                    PreviousCost = waveCost,
                };
                integrationQueue.Enqueue(queueElement);
                tile.Mark = IntegrationMark.Awaiting;
                integrationField[s] = tile;
            }
            if (westIsRelevant && wCost != byte.MaxValue)
            {
                IntegrationTile tile = integrationField[w];
                LOSQueueElement queueElement = new LOSQueueElement()
                {
                    Index = w,
                    PreviousCost = waveCost,
                };
                integrationQueue.Enqueue(queueElement);
                tile.Mark = IntegrationMark.Awaiting;
                integrationField[w] = tile;
            }
        }
        void DetermineLOSC(DirectionData directions)
        {
            int n = directions.N;
            int e = directions.E;
            int s = directions.S;
            int w = directions.W;
            //MARKS
            IntegrationMark nMark = integrationField[n].Mark;
            IntegrationMark eMark = integrationField[e].Mark;
            IntegrationMark sMark = integrationField[s].Mark;
            IntegrationMark wMark = integrationField[w].Mark;
            //RELEVANCIES
            bool northIsRelevant = nMark == IntegrationMark.Relevant;
            bool eastIsRelevant = eMark == IntegrationMark.Relevant;
            bool southIsRelevant = sMark == IntegrationMark.Relevant;
            bool westIsRelevant = wMark == IntegrationMark.Relevant;

            byte nCost = costs[n];
            byte eCost = costs[e];
            byte sCost = costs[s];
            byte wCost = costs[w];
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
        }
        void ApplyLOSBlockIfLOSCorner(int index)
        {
            //INDICIES
            int n = directions[index].N;
            int e = directions[index].E;
            int s = directions[index].S;
            int w = directions[index].W;
            int ne = directions[index].NE;
            int se = directions[index].SE;
            int sw = directions[index].SW;
            int nw = directions[index].NW;
            byte nCost = costs[n];
            byte eCost = costs[e];
            byte sCost = costs[s];
            byte wCost = costs[w];
            byte neCost = costs[ne];
            byte seCost = costs[se];
            byte swCost = costs[sw];
            byte nwCost = costs[nw];

            //is corner?
            bool isCornerFromNE = neCost != byte.MaxValue && nCost != byte.MaxValue && eCost != byte.MaxValue;
            bool isCornerFromSE = seCost != byte.MaxValue && sCost != byte.MaxValue && eCost != byte.MaxValue;
            bool isCornerFromSW = swCost != byte.MaxValue && sCost != byte.MaxValue && wCost != byte.MaxValue;
            bool isCornerFromNW = nwCost != byte.MaxValue && nCost != byte.MaxValue && wCost != byte.MaxValue;
            if (!IsCorner()) { return; }

            //corner data
            int2 source = To2D(initialWaveFront, fieldColAmount);
            int2 cornerIndex = To2D(index, fieldColAmount);
            int2 cornerDistance = new int2(source.x - cornerIndex.x, source.y - cornerIndex.y);
            int2 absCornerDistance = new int2(math.abs(cornerDistance.x), math.abs(cornerDistance.y));
            float2 cornerPos = new float2(cornerIndex.x * tileSize + tileSize / 2, cornerIndex.y * tileSize + tileSize / 2);
            float2 waveFrontTilePos = new float2(source.x * tileSize + tileSize / 2, source.y * tileSize + tileSize / 2);

            //evaluate for each corner direction
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
                const int ts = 1;
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
                stepAmount = new int2((int) p2Local.x, (int) p2Local.y);
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
                        points.Add(new float2(i / ts, y / ts));
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
                    int2 convergent = distanceDifference.x > 0 ? eIndex : nIndex;
                    int2 divergent = distanceDifference.x > 0 ? nIndex : eIndex;
                    float2 loscPosition = cornerPos + new float2(tileSize / 2, tileSize / 2);
                    cornerPositions.Add(loscPosition);
                    cornerTiles.Add(index);
                    float2 sourcePosition = waveFrontTilePos + new float2(-tileSize / 2, -tileSize / 2);
                    NativeList<int2> blockOffsets = GetOffsets(sourcePosition, loscPosition, out int2 stepAmount);
                    int2 step = divergent;
                    bool hitTheWall = false;
                    while (!IsOutOfBounds2D(step) && !hitTheWall)
                    {
                        for (int i = 0; i < blockOffsets.Length; i++)
                        {
                            int2 offset = blockOffsets[i];
                            int2 resultingIndex = offset + step;
                            int resultingIndex1d = To1D(resultingIndex, fieldColAmount);
                            IntegrationTile tile = integrationField[resultingIndex1d];
                            if (IsOutOfBounds2D(offset + step)) { break; }
                            if (tile.Mark == IntegrationMark.Irrelevant) { hitTheWall = true; break; }
                            if (costs[resultingIndex1d] == byte.MaxValue) { hitTheWall = true; break;  }
                            tile.Mark = IntegrationMark.LOSBlock;
                            integrationField[resultingIndex1d] = tile;
                        }
                        step += stepAmount;
                    }
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
                    int2 convergent = distanceDifference.x > 0 ? eIndex : sIndex;
                    int2 divergent = distanceDifference.x > 0 ? sIndex : eIndex;
                    float2 loscPosition = cornerPos + new float2(tileSize / 2, -tileSize / 2);
                    cornerPositions.Add(loscPosition);
                    cornerTiles.Add(index);
                    float2 sourcePosition = waveFrontTilePos + new float2(-tileSize / 2, tileSize / 2);
                    NativeList<int2> blockOffsets = GetOffsets(sourcePosition, loscPosition, out int2 stepAmount);
                    int2 step = divergent;
                    bool hitTheWall = false;
                    while (!IsOutOfBounds2D(step) && !hitTheWall)
                    {
                        for (int i = 0; i < blockOffsets.Length; i++)
                        {
                            int2 offset = blockOffsets[i];
                            int2 resultingIndex = offset + step;
                            int resultingIndex1d = To1D(resultingIndex, fieldColAmount);
                            IntegrationTile tile = integrationField[resultingIndex1d];
                            if (IsOutOfBounds2D(offset + step)) { break; }
                            if (tile.Mark == IntegrationMark.Irrelevant) { hitTheWall = true; break; }
                            if (costs[resultingIndex1d] == byte.MaxValue) { hitTheWall = true; break; }
                            tile.Mark = IntegrationMark.LOSBlock;
                            integrationField[resultingIndex1d] = tile;
                        }
                        step += stepAmount;
                    }
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
                    int2 convergent = distanceDifference.x > 0 ? wIndex : sIndex;
                    int2 divergent = distanceDifference.x > 0 ? sIndex : wIndex;
                    float2 loscPosition = cornerPos + new float2(-tileSize / 2, -tileSize / 2);
                    cornerPositions.Add(loscPosition);
                    cornerTiles.Add(index);
                    float2 sourcePosition = waveFrontTilePos + new float2(tileSize / 2, tileSize / 2);
                    NativeList<int2> blockOffsets = GetOffsets(sourcePosition, loscPosition, out int2 stepAmount);
                    int2 step = divergent;
                    bool hitTheWall = false;
                    while (!IsOutOfBounds2D(step) && !hitTheWall)
                    {
                        for (int i = 0; i < blockOffsets.Length; i++)
                        {
                            int2 offset = blockOffsets[i];
                            int2 resultingIndex = offset + step;
                            int resultingIndex1d = To1D(resultingIndex, fieldColAmount);
                            IntegrationTile tile = integrationField[resultingIndex1d];
                            if (IsOutOfBounds2D(offset + step)) { break; }
                            if (tile.Mark == IntegrationMark.Irrelevant) { hitTheWall = true; break; }
                            if (costs[resultingIndex1d] == byte.MaxValue) { hitTheWall = true; break; }
                            tile.Mark = IntegrationMark.LOSBlock;
                            integrationField[resultingIndex1d] = tile;
                        }
                        step += stepAmount;
                    }
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
                    int2 convergent = distanceDifference.x > 0 ? wIndex : nIndex;
                    int2 divergent = distanceDifference.x > 0 ? nIndex : wIndex;
                    float2 loscPosition = cornerPos + new float2(-tileSize / 2, tileSize / 2);
                    cornerPositions.Add(loscPosition);
                    cornerTiles.Add(index);
                    float2 sourcePosition = waveFrontTilePos + new float2(tileSize / 2, -tileSize / 2);
                    NativeList<int2> blockOffsets = GetOffsets(sourcePosition, loscPosition, out int2 stepAmount);
                    int2 step = divergent;
                    bool hitTheWall = false;
                    while (!IsOutOfBounds2D(step) && !hitTheWall)
                    {
                        for (int i = 0; i < blockOffsets.Length; i++)
                        {
                            int2 offset = blockOffsets[i];
                            int2 resultingIndex = offset + step;
                            int resultingIndex1d = To1D(resultingIndex, fieldColAmount);
                            IntegrationTile tile = integrationField[resultingIndex1d];
                            if (IsOutOfBounds2D(offset + step)) { break; }
                            if (tile.Mark == IntegrationMark.Irrelevant) { hitTheWall = true; break; }
                            if (costs[resultingIndex1d] == byte.MaxValue) { hitTheWall = true; break; }
                            tile.Mark = IntegrationMark.LOSBlock;
                            integrationField[resultingIndex1d] = tile;
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
public struct LOSQueueElement
{
    public int Index;
    public float PreviousCost;
}