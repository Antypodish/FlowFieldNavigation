using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;

[BurstCompile]
internal struct AvoidanceWallDetectionJob : IJobParallelFor
{
    internal float FieldMinXIncluding;
    internal float FieldMinYIncluding;
    internal float FieldMaxXExcluding;
    internal float FieldMaxYExcluding;
    internal float TileSize;
    internal int SectorColAmount;
    internal int SectorMatrixColAmount;
    internal int SectorTileAmount;
    internal float2 FieldGridStartPos;
    [ReadOnly] internal NativeArray<AgentMovementData> AgentMovementDataArray;
    [ReadOnly] internal NativeArray<UnsafeListReadOnly<byte>> CostFieldPerOffset;
    internal NativeArray<RoutineResult> RoutineResultArray;
    public void Execute(int index)
    {
        RoutineResult agentRoutineResult = RoutineResultArray[index];
        if (agentRoutineResult.NewAvoidance == 0) { return; }
        AgentMovementData agentMovementData = AgentMovementDataArray[index];
        float2 agentPos = new float2(agentMovementData.Position.x, agentMovementData.Position.z);
        int agentOffset = FlowFieldUtilities.RadiusToOffset(agentMovementData.Radius, TileSize);
        bool goingTowardsWall = IsGoingTowardsWall(agentPos, agentRoutineResult.NewDirection, agentOffset);
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
        float2 linecastPoint2 = agentPos + (checkDir * 1f);
        linecastPoint2.x = math.select(linecastPoint2.x, FieldMinXIncluding, linecastPoint2.x < FieldMinXIncluding);
        linecastPoint2.x = math.select(linecastPoint2.x, FieldMaxXExcluding - TileSize, linecastPoint2.x >= FieldMaxXExcluding);
        linecastPoint2.y = math.select(linecastPoint2.y, FieldMinYIncluding, linecastPoint2.y < FieldMinYIncluding);
        linecastPoint2.y = math.select(linecastPoint2.y, FieldMaxYExcluding - TileSize, linecastPoint2.y >= FieldMaxYExcluding);

        return LineCast(agentPos, linecastPoint2, offset);
    }
    bool LineCast(float2 start, float2 end, int offset)
    {
        UnsafeListReadOnly<byte> costs = CostFieldPerOffset[offset];

        start += math.select(0f, 0.0001f, start.x == end.x);
        float2 leftPoint = math.select(end, start, start.x < end.x);
        float2 rigthPoint = math.select(start, end, start.x < end.x);
        float xMin = leftPoint.x;
        float xMax = rigthPoint.x;
        int2 leftIndex = FlowFieldUtilities.PosTo2D(leftPoint, TileSize, FieldGridStartPos);
        int2 rightIndex = FlowFieldUtilities.PosTo2D(rigthPoint, TileSize, FieldGridStartPos);

        float deltaX = (leftPoint.x - rigthPoint.x);
        float x1 = rigthPoint.x;
        float deltaY = (leftPoint.y - rigthPoint.y);
        float y1 = rigthPoint.y;
        for (int xIndex = leftIndex.x; xIndex <= rightIndex.x; xIndex++)
        {
            float xLeft = FieldGridStartPos.x + xIndex * TileSize;
            float xRight = xLeft + TileSize;
            xLeft = math.max(xLeft, xMin);
            xRight = math.min(xRight, xMax);

            float tLeft = (xLeft - x1) / deltaX;
            float tRight = (xRight - x1) / deltaX;
            float yLeft = y1 + deltaY * tLeft;
            float yRight = y1 + deltaY * tRight;

            int yIndexLeft = (int)math.floor((yLeft - FieldGridStartPos.y) / TileSize);
            int yIndexRight = (int)math.floor((yRight - FieldGridStartPos.y) / TileSize);
            int yIndexMin = math.min(yIndexLeft, yIndexRight);
            int yIndexMax = math.max(yIndexLeft, yIndexRight);

            for (int yIndex = yIndexMin; yIndex <= yIndexMax; yIndex++)
            {
                int2 indexToPlot = new int2(xIndex, yIndex);
                LocalIndex1d localToPlot = FlowFieldUtilities.GetLocal1D(indexToPlot, SectorColAmount, SectorMatrixColAmount);
                if (costs[localToPlot.sector * SectorTileAmount + localToPlot.index] == byte.MaxValue)
                {
                    return true;
                }
            }
        }
        return false;
    }
}
