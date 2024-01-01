﻿using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;

[BurstCompile]
public struct AvoidanceWallDetectionJob : IJobParallelFor
{
    public float FieldMinXIncluding;
    public float FieldMinYIncluding;
    public float FieldMaxXExcluding;
    public float FieldMaxYExcluding;
    public float TileSize;
    public int SectorColAmount;
    public int SectorMatrixColAmount;
    public int SectorTileAmount;
    [ReadOnly] public NativeArray<AgentMovementData> AgentMovementDataArray;
    [ReadOnly] public NativeArray<UnsafeListReadOnly<byte>> CostFieldPerOffset;
    public NativeArray<RoutineResult> RoutineResultArray;
    public void Execute(int index)
    {
        RoutineResult agentRoutineResult = RoutineResultArray[index];
        if (agentRoutineResult.NewAvoidance == 0) { return; }
        AgentMovementData agentMovementData = AgentMovementDataArray[index];
        float2 agentPos = new float2(agentMovementData.Position.x, agentMovementData.Position.z);
        bool goingTowardsWall = IsGoingTowardsWall(agentPos, agentRoutineResult.NewDirection, agentMovementData.Offset);
        if (goingTowardsWall)
        {
            agentRoutineResult.NewAvoidance = agentRoutineResult.NewAvoidance == AvoidanceStatus.L ? AvoidanceStatus.R : AvoidanceStatus.L;
            agentRoutineResult.NewSplitInfo = 50;
            agentRoutineResult.NewDirection = -agentRoutineResult.NewDirection;
            RoutineResultArray[index] = agentRoutineResult;
        }
    }

    bool IsGoingTowardsWall(float2 agentPos, float2 checkDir, int offset)
    {
        float2 linecastPoint2 = agentPos + (checkDir * 0.6f);
        linecastPoint2.x = math.select(linecastPoint2.x, FieldMinXIncluding, linecastPoint2.x < FieldMinXIncluding);
        linecastPoint2.x = math.select(linecastPoint2.x, FieldMaxXExcluding - TileSize, linecastPoint2.x >= FieldMaxXExcluding);
        linecastPoint2.y = math.select(linecastPoint2.y, FieldMinYIncluding, linecastPoint2.y < FieldMinYIncluding);
        linecastPoint2.y = math.select(linecastPoint2.y, FieldMaxYExcluding - TileSize, linecastPoint2.y >= FieldMaxYExcluding);

        return LineCast(agentPos, linecastPoint2, offset);
    }
    bool LineCast(float2 point1, float2 point2, int offset)
    {
        UnsafeListReadOnly<byte> costs = CostFieldPerOffset[offset];
        float2 leftPoint = math.select(point2, point1, point1.x < point2.x);
        float2 rightPoint = math.select(point1, point2, point1.x < point2.x);
        float2 dif = rightPoint - leftPoint;
        float slope = dif.y / dif.x;
        float c = leftPoint.y - (slope * leftPoint.x);
        int2 point1Index = (int2)math.floor(point1 / TileSize);
        int2 point2Index = (int2)math.floor(point2 / TileSize);
        if (point1Index.x == point2Index.x || dif.x == 0)
        {
            int startY = (int)math.floor(math.select(point2.y, point1.y, point1.y < point2.y) / TileSize);
            int endY = (int)math.floor(math.select(point2.y, point1.y, point1.y > point2.y) / TileSize);
            for (int y = startY; y <= endY; y++)
            {
                int2 index = new int2(point1Index.x, y);
                LocalIndex1d local = FlowFieldUtilities.GetLocal1D(index, SectorColAmount, SectorMatrixColAmount);
                if (costs[local.sector * SectorTileAmount + local.index] == byte.MaxValue) { return true; }
            }
            return false;
        }
        if (dif.y == 0)
        {
            int startX = (int)math.floor(math.select(point2.x, point1.x, point1.x < point2.x) / TileSize);
            int endX = (int)math.floor(math.select(point2.x, point1.x, point1.x > point2.x) / TileSize);
            for (int x = startX; x <= endX; x++)
            {
                int2 index = new int2(x, point1Index.y);
                LocalIndex1d local = FlowFieldUtilities.GetLocal1D(index, SectorColAmount, SectorMatrixColAmount);
                if (costs[local.sector * SectorTileAmount + local.index] == byte.MaxValue) { return true; }
            }
            return false;
        }


        //HANDLE START
        float2 startPoint = leftPoint;
        float nextPointX = math.ceil(startPoint.x / TileSize) * TileSize;
        float2 nextPoint = new float2(nextPointX, c + slope * nextPointX);
        int2 startIndex = (int2)math.floor(startPoint / TileSize);
        int2 nextIndex = (int2)math.floor(nextPoint / TileSize);
        int minY = math.select(nextIndex.y, startIndex.y, startIndex.y < nextIndex.y);
        int maxY = math.select(startIndex.y, nextIndex.y, startIndex.y < nextIndex.y);
        for (int y = minY; y <= maxY; y++)
        {
            int2 index = new int2(startIndex.x, y);
            LocalIndex1d local = FlowFieldUtilities.GetLocal1D(index, SectorColAmount, SectorMatrixColAmount);
            if (costs[local.sector * SectorTileAmount + local.index] == byte.MaxValue) { return true; }
        }

        //HANDLE END
        float2 endPoint = rightPoint;
        float prevPointX = math.floor(endPoint.x / TileSize) * TileSize;
        float2 prevPoint = new float2(prevPointX, c + slope * prevPointX);
        int2 endIndex = (int2)math.floor(endPoint / TileSize);
        int2 prevIndex = (int2)math.floor(prevPoint / TileSize);
        minY = math.select(prevIndex.y, endIndex.y, endIndex.y < prevIndex.y);
        maxY = math.select(endIndex.y, prevIndex.y, endIndex.y < prevIndex.y);
        for (int y = minY; y <= maxY; y++)
        {
            int2 index = new int2(endIndex.x, y);
            LocalIndex1d local = FlowFieldUtilities.GetLocal1D(index, SectorColAmount, SectorMatrixColAmount);
            if (costs[local.sector * SectorTileAmount + local.index] == byte.MaxValue) { return true; }
        }

        //HANDLE MIDDLE
        float curPointY = nextPoint.y;
        float curPointX = nextPoint.x;
        int curIndexX = nextIndex.x;
        int stepCount = (endIndex.x - startIndex.x) - 1;
        for (int i = 0; i < stepCount; i++)
        {
            float newPointX = curPointX + TileSize;
            float newtPointY = slope * newPointX + c;
            int curIndexY = (int)math.floor(curPointY / TileSize);
            int newIndexY = (int)math.floor(newtPointY / TileSize);
            minY = math.select(curIndexY, newIndexY, newIndexY < curIndexY);
            maxY = math.select(newIndexY, curIndexY, newIndexY < curIndexY);
            for (int y = minY; y <= maxY; y++)
            {
                int2 index = new int2(curIndexX, y);
                LocalIndex1d local = FlowFieldUtilities.GetLocal1D(index, SectorColAmount, SectorMatrixColAmount);
                if (costs[local.sector * SectorTileAmount + local.index] == byte.MaxValue) { return true; }
            }
            curIndexX++;
            curPointY = newtPointY;
            curPointX = newPointX;
        }
        return false;
    }
}
