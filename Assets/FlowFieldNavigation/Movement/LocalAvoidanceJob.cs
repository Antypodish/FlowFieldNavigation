
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
public struct LocalAvoidanceJob : IJobParallelFor
{
    public float FieldMinXIncluding;
    public float FieldMinYIncluding;
    public float FieldMaxXExcluding;
    public float FieldMaxYExcluding;
    public float TileSize;
    public int SectorColAmount;
    public int SectorMatrixColAmount;
    public int SectorTileAmount;
    public float SeekMultiplier;
    public float AlignmentMultiplier;
    public float SeperationMultiplier;
    public float SeperationRangeAddition;
    public float AlignmentRangeAddition;
    public float MovingAvoidanceRangeAddition;
    public float BaseSpatialGridSize;
    public float FieldHorizontalSize;
    public float FieldVerticalSize;
    [ReadOnly] public NativeArray<UnsafeListReadOnly<byte>> CostFieldEachOffset;
    [ReadOnly] public AgentSpatialHashGrid AgentSpatialHashGrid;
    [WriteOnly] public NativeArray<RoutineResult> RoutineResultArray;

    public void Execute(int index)
    {
        AgentMovementData agent = AgentSpatialHashGrid.RawAgentMovementDataArray[index];
        RoutineResult newRoutineResult = new RoutineResult()
        {
            NewAvoidance = agent.Avoidance,
            NewSplitInfo = agent.SplitInfo,
            NewSplitInterval = agent.SplitInterval,
            NewMovingAvoidance = agent.MovingAvoidance,
        };

        float3 agentPos3 = agent.Position;
        float2 agentPos2 = new float2(agent.Position.x, agent.Position.z);
        float2 newDirectionToSteer = agent.DesiredDirection;

        if (HasStatusFlag(AgentStatus.HoldGround, agent.Status)) { return; }
        //GET AVOIDANCE STATUS
        if (newRoutineResult.NewAvoidance == 0)
        {
            newRoutineResult.NewAvoidance = GetAvoidanceStatus(agentPos2, agent.DesiredDirection, agent.Radius, index);
            if (newRoutineResult.NewAvoidance != 0)
            {
                newRoutineResult.NewSplitInterval = 50;
            }
        }

        //CHECK IF DESIRED DIRECTION IS FREE
        if (newRoutineResult.NewAvoidance != 0 && math.dot(agent.DesiredDirection, agent.CurrentDirection) > 0)
        {
            newRoutineResult.NewAvoidance = IsDirectionFree(agentPos2, agent.DesiredDirection, index, agent.Radius, agent.Radius + 1f) ? 0 : newRoutineResult.NewAvoidance;
        }

        //GET AVIODANCE DIRECTION
        if (newRoutineResult.NewAvoidance != 0)
        {
            float2 avoidanceDirection = GetAvoidanceDirection(agentPos2, agent.CurrentDirection, agent.Radius, index, agent.Radius + 1f, newRoutineResult.NewAvoidance);
            newDirectionToSteer = avoidanceDirection;
            if (avoidanceDirection.Equals(0))
            {
                newRoutineResult.NewAvoidance = 0;
            }
        }

        //GET MOVING AVOIDANCE
        float2 movingAvoidance = GetMovingAvoidance(agentPos2, agent.CurrentDirection, index, agent.Radius, ref newRoutineResult.NewMovingAvoidance, agent.PathId);
        if (!movingAvoidance.Equals(0))
        {
            newDirectionToSteer = movingAvoidance;
        }
        //GET SEPERATION
        float3 seperation = GetSeperation(agentPos3, agent.CurrentDirection, agent.Radius, index, newRoutineResult.NewAvoidance, agent.PathId, out newRoutineResult.HasForeignInFront);

        //GET ALIGNMENT
        if (newRoutineResult.NewAvoidance == 0 && movingAvoidance.Equals(0))
        {
            float2 alignment = GetAlignment(agentPos2, agent.DesiredDirection, agent.CurrentDirection, index, agent.FlockIndex, agent.Radius, agent.Offset, agent.AlignmentMultiplierPercentage);
            newDirectionToSteer += alignment;
        }
        if(!HasStatusFlag(AgentStatus.Moving, agent.Status))
        {
            float2 stoppedSeperation = GetStoppedSeperationForce(agentPos3, agent.CurrentDirection, agent.Radius, index);
            newDirectionToSteer = math.select(stoppedSeperation, agent.CurrentDirection, stoppedSeperation.Equals(0));
        }

        //GET SEEK
        float2 seek = GetSeek(agent.CurrentDirection, newDirectionToSteer, agent.Status);

        //COMBINE FORCES
        if (HasStatusFlag(AgentStatus.Moving, agent.Status))
        {
            newRoutineResult.NewDirection = agent.CurrentDirection + seek;
            newRoutineResult.NewSeperation = seperation;
        }
        else
        {
            newRoutineResult.NewDirection = agent.CurrentDirection + seek;
        }
        RoutineResultArray[index] = newRoutineResult;
    }

    float2 GetSeek(float2 currentDirection, float2 desiredDirection, AgentStatus agentStatus)
    {
        float2 steeringToSeek = desiredDirection - currentDirection;
        float steeringToSeekLen = math.length(steeringToSeek);
        float seekmultiplier = math.select(SeekMultiplier, 0.05f, agentStatus == 0);
        return math.select(steeringToSeek / steeringToSeekLen, 0f, steeringToSeekLen == 0) * math.select(seekmultiplier, steeringToSeekLen, steeringToSeekLen < seekmultiplier);
    }
    float2 GetAlignment(float2 agentPos, float2 desiredDirection, float2 currentDirection, int agentIndex, int agentFlockIndex, float radius, int offset, float alignmentMultiplierPercentage)
    {
        float2 totalHeading = 0;
        int alignedAgentCount = 0;

        float2 toalCurrentHeading = 0;
        int curAlignedAgentCount = 0;
        bool avoiding = false;

        float checkRange = radius + AlignmentRangeAddition;
        for(int i = 0; i < AgentSpatialHashGrid.GetGridCount(); i++)
        {
            SpatialHashGridIterator iterator = AgentSpatialHashGrid.GetIterator(agentPos, checkRange, i);
            while (iterator.HasNext())
            {
                NativeSlice<AgentMovementData> agentsToCheck = iterator.GetNextRow(out int sliceStart);
                for(int j = 0; j < agentsToCheck.Length; j++)
                {
                    AgentMovementData mate = agentsToCheck[j];
                    float2 matePos = new float2(mate.Position.x, mate.Position.z);
                    float distance = math.distance(agentPos, matePos);
                    float desiredDistance = mate.Radius + checkRange;
                    float overlapping = desiredDistance - distance;

                    if (sliceStart + j == agentIndex) { continue; }
                    if (math.dot(matePos - agentPos, desiredDirection) < 0) { continue; }
                    if (math.dot(mate.CurrentDirection, currentDirection) <= 0) { continue; }
                    if (!HasStatusFlag(AgentStatus.Moving, mate.Status)) { continue; }
                    if (overlapping <= 0) { continue; }
                    if (IsNotAlignable(agentPos, mate.DesiredDirection, offset)) { continue; }
                    if (mate.FlockIndex == agentFlockIndex && math.dot(mate.DesiredDirection, desiredDirection) > 0.5f)
                    {
                        totalHeading += mate.DesiredDirection;
                        alignedAgentCount = math.select(alignedAgentCount + 1, alignedAgentCount, mate.DesiredDirection.Equals(0));
                    }
                    toalCurrentHeading += math.normalize(mate.CurrentDirection);
                    curAlignedAgentCount = math.select(curAlignedAgentCount + 1, curAlignedAgentCount, mate.CurrentDirection.Equals(0));
                    if (mate.Avoidance != 0) { avoiding = true; }
                }
            }
        }
        if (avoiding)
        {
            return math.select((toalCurrentHeading / curAlignedAgentCount - desiredDirection) * AlignmentMultiplier * alignmentMultiplierPercentage, 0, curAlignedAgentCount == 0);
        }
        return math.select((totalHeading / alignedAgentCount - desiredDirection) * alignmentMultiplierPercentage, 0, alignedAgentCount == 0);
    }
    float3 GetSeperation(float3 agentPos3, float2 currentDirection, float agentRadius, int agentIndex, AvoidanceStatus agentAvoidance, int pathId, out bool hasForeignInFront)
    {
        float2 agentPos2 = new float2(agentPos3.x, agentPos3.z);
        bool agentIsAvoiding = agentAvoidance != 0;

        float3 totalFrontSeperation = 0;
        float3 totalBackSeperation = 0;
        float3 totalAvoidanceBackSeperation = 0;
        int frontSeperationCount = 0;
        int backSeperationCount = 0;
        int totalAvoidanceBackSeperationCount = 0;
        bool hasForeignAgentAround = false;
        float checkRange = agentRadius + SeperationRangeAddition + 0.1f;
        for (int i = 0; i < AgentSpatialHashGrid.GetGridCount(); i++)
        {
            SpatialHashGridIterator iterator = AgentSpatialHashGrid.GetIterator(agentPos2, checkRange, i);
            while (iterator.HasNext())
            {
                NativeSlice<AgentMovementData> agentsToCheck = iterator.GetNextRow(out int sliceStart);
                for (int j = 0; j < agentsToCheck.Length; j++)
                {
                    AgentMovementData mate = agentsToCheck[j];
                    float3 matePos3 = mate.Position;
                    float2 matePos2 = new float2(mate.Position.x, mate.Position.z);
                    float distance = math.distance(matePos3, agentPos3);
                    bool mateMoving = HasStatusFlag(AgentStatus.Moving, mate.Status);

                    //FOREIGN CHECK
                    float desiredForeignRange = mate.Radius + checkRange;
                    bool samePath = mate.PathId == pathId;
                    bool mateMovingOppositeDirection = math.dot(currentDirection, mate.CurrentDirection) < 0;
                    hasForeignAgentAround |= (desiredForeignRange - distance) > 0 && !samePath && mateMoving && mateMovingOppositeDirection;

                    //SEPERATION CHECK
                    float desiredSeperationRange = mate.Radius + checkRange - 0.1f;
                    float seperationOverlapping = desiredSeperationRange - distance;
                    bool mateIsAvoidingNot = mate.Avoidance == 0;
                    if (seperationOverlapping <= 0) { continue; }
                    if (!mateMoving) { continue; }
                    if (agentIsAvoiding && mateIsAvoidingNot) { continue; }
                    if(math.dot(currentDirection, matePos2 - agentPos2) < 0)
                    {
                        float3 seperationDirection = math.normalizesafe(agentPos3 - matePos3);

                        float normalMultiplier = math.lerp(0, seperationOverlapping, seperationOverlapping / (desiredForeignRange * 0.3f));
                        float3 seperationForce = seperationDirection * normalMultiplier;
                        seperationForce = math.select(seperationForce, new float3(sliceStart + j, 0, 1), agentPos3.Equals(matePos3) && agentIndex < sliceStart + j);
                        totalBackSeperation += seperationForce;
                        backSeperationCount++;

                        float avoidingMultiplier = math.lerp(0, seperationOverlapping, seperationOverlapping / (desiredForeignRange * 0.4f));
                        seperationForce = seperationDirection * avoidingMultiplier;
                        seperationForce = math.select(seperationForce, new float3(sliceStart + j, 0, 1), agentPos3.Equals(matePos3) && agentIndex < sliceStart + j);
                        totalAvoidanceBackSeperation += seperationForce;
                        totalAvoidanceBackSeperationCount++;
                    }
                    else
                    {
                        float frontMultiplier = math.lerp(0, seperationOverlapping, seperationOverlapping / (desiredForeignRange * 0.5f));
                        float3 seperationForce = math.normalizesafe(agentPos3 - matePos3) * frontMultiplier;
                        seperationForce = math.select(seperationForce, new float3(sliceStart + j, 0, 1), agentPos3.Equals(matePos3) && agentIndex < sliceStart + j);
                        totalFrontSeperation += seperationForce;
                        frontSeperationCount++;
                    }

                }
            }
        }
        hasForeignInFront = hasForeignAgentAround;
        float3 totalSeperation = math.select(totalFrontSeperation + totalBackSeperation, totalFrontSeperation + totalAvoidanceBackSeperation, hasForeignAgentAround);
        int seperationCount = math.select(frontSeperationCount + backSeperationCount, frontSeperationCount + totalAvoidanceBackSeperationCount, hasForeignAgentAround);
        if (seperationCount == 0) { return 0; }
        totalSeperation /= seperationCount;
        return totalSeperation * math.select(SeperationMultiplier, SeperationMultiplier * 0.4f, hasForeignAgentAround);
    }

    //That job is so important for smoothing out collisions for stopped agents
    float2 GetStoppedSeperationForce(float3 agentPos3, float2 agentDir, float agentRadius, int agentIndex)
    {
        float2 agentPos2 = new float2(agentPos3.x, agentPos3.z);
        float2 totalSeperation = 0;
        float checkRange = agentRadius - 0.05f;
        for (int i = 0; i < AgentSpatialHashGrid.GetGridCount(); i++)
        {
            SpatialHashGridIterator iterator = AgentSpatialHashGrid.GetIterator(agentPos2, checkRange, i);
            while (iterator.HasNext())
            {
                NativeSlice<AgentMovementData> agentsToCheck = iterator.GetNextRow(out int sliceStart);
                for (int j = 0; j < agentsToCheck.Length; j++)
                {
                    AgentMovementData mateData = agentsToCheck[j];
                    if (sliceStart + j == agentIndex) { continue; }

                    float3 matePos3 = mateData.Position;
                    float2 matePos2 = new float2(mateData.Position.x, mateData.Position.z);
                    float distance = math.distance(matePos3, agentPos3);

                    float seperationRadius = checkRange + mateData.Radius;
                    if (distance > seperationRadius) { continue; }
                    float overlapping = seperationRadius - distance;
                    float2 seperation = agentPos2 - matePos2;

                    if (math.dot(agentDir, matePos2 - agentPos2) >= 0 && !HasStatusFlag(AgentStatus.Moving, mateData.Status)) { overlapping*=0.3f; }
                    seperation = math.normalizesafe(seperation) * overlapping;
                    totalSeperation += seperation;
                }
            }
        }
        return totalSeperation;
    }
    AvoidanceStatus GetAvoidanceStatus(float2 agentPos, float2 desiredDirection, float agentRadius, int agentIndex)
    {
        float checkRange = agentRadius + SeperationRangeAddition + 0.1f;
        for (int i = 0; i < AgentSpatialHashGrid.GetGridCount(); i++)
        {
            SpatialHashGridIterator iterator = AgentSpatialHashGrid.GetIterator(agentPos, checkRange, i);
            while (iterator.HasNext())
            {
                NativeSlice<AgentMovementData> agentsToCheck = iterator.GetNextRow(out int sliceStart);
                for (int j = 0; j < agentsToCheck.Length; j++)
                {
                    AgentMovementData mateData = agentsToCheck[j];
                    if (sliceStart + j == agentIndex) { continue; }
                    if ((mateData.Status & AgentStatus.HoldGround) != AgentStatus.HoldGround) { continue; }

                    float2 matePos = new float2(mateData.Position.x, mateData.Position.z);
                    float distance = math.distance(matePos, agentPos);

                    float obstacleDetectionRange = mateData.Radius + checkRange;

                    if (distance > obstacleDetectionRange) { continue; }

                    float dot = math.dot(desiredDirection, matePos - agentPos);
                    if (dot <= 0f) { continue; }

                    return DetermineAvoidance(agentPos, desiredDirection, agentRadius, agentIndex);
                }
            }
        }
        return 0;        
    }
    AvoidanceStatus DetermineAvoidance(float2 agentPos, float2 desiredDirection, float agentRadius, int agentIndex)
    {
        float2 totalLeftAvoiance = 0;
        float2 totalRightAvoidance = 0;
        float checkRange = agentRadius + 0.5f;
        for (int i = 0; i < AgentSpatialHashGrid.GetGridCount(); i++)
        {
            SpatialHashGridIterator iterator = AgentSpatialHashGrid.GetIterator(agentPos, checkRange, i);
            while (iterator.HasNext())
            {
                NativeSlice<AgentMovementData> agentsToCheck = iterator.GetNextRow(out int sliceStart);
                for (int j = 0; j < agentsToCheck.Length; j++)
                {
                    AgentMovementData mateData = agentsToCheck[j];
                    if (sliceStart + j == agentIndex) { continue; }
                    if ((mateData.Status & AgentStatus.HoldGround) != AgentStatus.HoldGround) { continue; }
                    float2 matePos = new float2(mateData.Position.x, mateData.Position.z);
                    float distance = math.distance(matePos, agentPos);
                    float obstacleDetectionRange = (agentRadius + mateData.Radius) + 0.2f + 3f;
                    if (distance > obstacleDetectionRange) { continue; }
                    float2 mateDir = matePos - agentPos;
                    float dot = math.dot(desiredDirection, math.normalizesafe(mateDir));
                    if (dot <= 0) { continue; }
                    float2 leftAvoidance = math.normalizesafe(new float2(-mateDir.y, mateDir.x));
                    float2 rightAvoidance = math.normalizesafe(new float2(mateDir.y, -mateDir.x));
                    totalLeftAvoiance += leftAvoidance;
                    totalRightAvoidance += rightAvoidance;

                }
            }
        }
        totalLeftAvoiance = math.normalizesafe(totalLeftAvoiance);
        totalRightAvoidance = math.normalizesafe(totalRightAvoidance);
        float leftDot = math.dot(totalLeftAvoiance, desiredDirection);
        float rightDot = math.dot(totalRightAvoidance, desiredDirection);
        return leftDot > rightDot ? AvoidanceStatus.L : AvoidanceStatus.R;
    }
    float2 GetAvoidanceDirection(float2 agentPos, float2 currentDirection, float agentRadius, int agentIndex, float maxCheckRange, AvoidanceStatus agentAvoidance)
    {
        float2 closestObstaclePos = 0;
        float closestObstacleRadius = 0;
        float closesObstacleDotProduct = float.MinValue;
        float checkRange = agentRadius + SeperationRangeAddition + 1f;
        for (int i = 0; i < AgentSpatialHashGrid.GetGridCount(); i++)
        {
            SpatialHashGridIterator iterator = AgentSpatialHashGrid.GetIterator(agentPos, checkRange, i);
            while (iterator.HasNext())
            {
                NativeSlice<AgentMovementData> agentsToCheck = iterator.GetNextRow(out int sliceStart);
                for (int j = 0; j < agentsToCheck.Length; j++)
                {
                    AgentMovementData mateData = agentsToCheck[j];
                    if (sliceStart + j == agentIndex) { continue; }
                    if ((mateData.Status & AgentStatus.HoldGround) != AgentStatus.HoldGround) { continue; }
                    float2 matePos = new float2(mateData.Position.x, mateData.Position.z);
                    float distance = math.distance(matePos, agentPos);
                    float obstacleDetectionRange = (agentRadius + mateData.Radius) + SeperationRangeAddition + 1f;
                    if (distance > obstacleDetectionRange) { continue; }
                    float2 mateDir = matePos - agentPos;
                    float dot = math.dot(currentDirection, math.normalizesafe(mateDir));
                    if (dot < -1f) { continue; }

                    if (dot > closesObstacleDotProduct)
                    {
                        closestObstaclePos = matePos;
                        closestObstacleRadius = mateData.Radius;
                        closesObstacleDotProduct = dot;
                    }

                }
            }
        }
        if (closesObstacleDotProduct == float.MinValue) { return 0; }
        float totalRadius = closestObstacleRadius + agentRadius + SeperationRangeAddition;
        float2 destinationPoint = agentAvoidance == AvoidanceStatus.L ? GetLeftDirection(agentPos, closestObstaclePos, currentDirection, totalRadius) : GetRightDirection(agentPos, closestObstaclePos, currentDirection, totalRadius);
        return math.normalizesafe(destinationPoint - agentPos);

        float2 GetLeftDirection(float2 agentPos, float2 circleCenter, float2 agentDirection, float circleRadius)
        {
            float2 obstacleRelativePos = circleCenter - agentPos;
            float obstacleDistance = math.length(obstacleRelativePos);
            float2 agentDirectionResized = agentDirection * obstacleDistance;
            float2 newDirection = agentDirectionResized;
            float dotRotated = obstacleRelativePos.x * -agentDirectionResized.y + obstacleRelativePos.y * agentDirectionResized.x;
            if (dotRotated > 0)
            {
                obstacleRelativePos = obstacleRelativePos / obstacleDistance;
                agentDirectionResized = agentDirectionResized / obstacleDistance;
                float cos = math.dot(obstacleRelativePos, agentDirectionResized);
                float sin = math.sqrt(1 - cos * cos);
                newDirection = new float2(cos * obstacleRelativePos.x - sin * obstacleRelativePos.y, sin * obstacleRelativePos.x + cos * obstacleRelativePos.y) * obstacleDistance;
            }
            float2 newWorldDirection = agentPos + math.select(newDirection, math.normalizesafe(newDirection) * maxCheckRange, obstacleDistance > maxCheckRange);
            float2 snap = newWorldDirection - circleCenter;
            snap = math.normalizesafe(snap) * circleRadius;
            return circleCenter + snap;
        }
        float2 GetRightDirection(float2 agentPos, float2 circleCenter, float2 agentDirection, float circleRadius)
        {
            float2 obstacleRelativePos = circleCenter - agentPos;
            float obstacleDistance = math.length(obstacleRelativePos);
            float2 agentDirectionResized = agentDirection * obstacleDistance;
            float2 newDirection = agentDirectionResized;
            float dotRotated = obstacleRelativePos.x * -agentDirectionResized.y + obstacleRelativePos.y * agentDirectionResized.x;
            if (dotRotated < 0)
            {
                obstacleRelativePos = math.normalizesafe(obstacleRelativePos);
                agentDirectionResized = math.normalizesafe(agentDirectionResized);
                float cos = -1 * math.dot(obstacleRelativePos, agentDirectionResized);
                float sin = math.sqrt(1 - cos * cos);
                obstacleRelativePos *= -1;
                newDirection = new float2(cos * obstacleRelativePos.x - sin * obstacleRelativePos.y, sin * obstacleRelativePos.x + cos * obstacleRelativePos.y) * obstacleDistance;
            }
            float2 newWorldDirection = agentPos + math.select(newDirection, math.normalizesafe(newDirection) * maxCheckRange, obstacleDistance > maxCheckRange);
            float2 snap = newWorldDirection - circleCenter;
            snap = math.normalizesafe(snap) * circleRadius;
            return circleCenter + snap;
        }
    }
    bool IsDirectionFree(float2 agentPos, float2 direction, int agentIndex, float agentRadius, float checkRange)
    {
        float2 directionClampedToRange = math.normalizesafe(direction) * checkRange;
        float2 directionPerpLeft = new float2(-directionClampedToRange.y, directionClampedToRange.x) * agentRadius;
        float2 directionPerpRight = new float2(directionClampedToRange.y, -directionClampedToRange.x) * agentRadius;

        float2 leftLinep1 = agentPos + directionPerpLeft;
        float2 leftLinep2 = agentPos + directionClampedToRange + directionPerpLeft;
        
        float2 rightLinep1 = agentPos + directionPerpRight;
        float2 rightLinep2 = agentPos + directionClampedToRange + directionPerpRight;

        for (int i = 0; i < AgentSpatialHashGrid.GetGridCount(); i++)
        {
            SpatialHashGridIterator iterator = AgentSpatialHashGrid.GetIterator(agentPos, checkRange, i);
            while (iterator.HasNext())
            {
                NativeSlice<AgentMovementData> agentsToCheck = iterator.GetNextRow(out int sliceStart);
                for (int j = 0; j < agentsToCheck.Length; j++)
                {
                    AgentMovementData mate = agentsToCheck[j];
                    if (agentIndex == j + sliceStart) { continue; }
                    if (!HasStatusFlag(AgentStatus.HoldGround, mate.Status)) { continue; }
                    float3 matePos3 = mate.Position;
                    float2 matePos2 = new float2(matePos3.x, matePos3.z);
                    if (math.distance(agentPos, matePos2) > checkRange + mate.Radius) { continue; }
                    float2 dirFromLeft = matePos2 - leftLinep1;
                    float2 dirFromRight = matePos2 - rightLinep1;
                    bool isRightOfLeft = math.dot(leftLinep2 - leftLinep1, new float2(dirFromLeft.y, -dirFromLeft.x)) < 0;
                    bool isLeftOfRight = math.dot(rightLinep2 - rightLinep1, new float2(-dirFromRight.y, dirFromRight.x)) < 0;
                    if (Intersects(leftLinep1, leftLinep2, matePos2, mate.Radius) || Intersects(rightLinep1, rightLinep2, matePos2, mate.Radius) || (isLeftOfRight && isRightOfLeft && math.dot(direction, matePos2 - agentPos) >= 0))
                    {
                        return false;
                    }

                }
            }
        }
        return true;
    }
    bool HasStatusFlag(AgentStatus flag, AgentStatus agentStatus)
    {
        return (agentStatus & flag) == flag;
    }
    bool Intersects(float2 linep1, float2 linep2, float2 matePos, float mateRadius)
    {
        if (linep1.y == linep2.y) //M = 0
        {
            float2 rh = math.select(linep2, linep1, linep1.x > linep2.x);
            float2 lh = math.select(linep2, linep1, linep1.x < linep2.x);
            if (matePos.x < rh.x && matePos.x > lh.x)
            {
                float yDistance = math.abs(lh.y - matePos.y);
                return yDistance <= mateRadius;
            }
            float lhDistance = math.distance(lh, matePos);
            float rhDistance = math.distance(rh, matePos);
            return rhDistance <= mateRadius || lhDistance <= mateRadius;
        }
        else if (linep1.x == linep2.x) //M = INFINITY
        {
            float2 up = math.select(linep2, linep1, linep1.y > linep2.y);
            float2 down = math.select(linep2, linep1, linep1.y < linep2.y);
            if (matePos.y < up.y && matePos.y > down.y)
            {
                float xDistance = math.abs(down.x - matePos.x);
                return xDistance <= mateRadius;
            }
            float upDistance = math.distance(down, matePos);
            float downDistance = math.distance(up, matePos);
            return downDistance <= mateRadius || upDistance <= mateRadius;
        }
        else
        {
            float2 rh = math.select(linep2, linep1, linep1.x > linep2.x);
            float2 lh = math.select(linep2, linep1, linep1.x < linep2.x);

            float m1 = (rh.y - lh.y) / (rh.x - lh.x);
            float m2 = -m1;

            float c1 = rh.y - m1 * rh.x;
            float c2 = matePos.y - m2 * matePos.x;

            float x = (c2 - c1) / (2 * m1);
            float y = (c2 - c1) / 2 + c1;

            float2 intersectionPoint = new float2(x, y);

            if (intersectionPoint.x <= rh.x && intersectionPoint.x >= lh.x)
            {
                return math.distance(intersectionPoint, matePos) < mateRadius;
            }
            return math.distance(matePos, lh) < mateRadius || math.distance(matePos, rh) < mateRadius;
        }
    }
    float2 GetMovingAvoidance(float2 agentPos, float2 agentCurrentDir, int agentIndex, float agentRadius, ref MovingAvoidanceStatus avoidance, int pathId)
    {
        float2 center1 = agentPos;
        float2 center2 = agentPos + math.normalizesafe(agentCurrentDir) * (agentRadius + 0.2f);

        float2 rightEdgeOffset = math.normalizesafe(new float2(-agentCurrentDir.y, agentCurrentDir.x)) * agentRadius + SeperationRangeAddition / 2;
        float2 leftEdgeOffset = math.normalizesafe(new float2(agentCurrentDir.y, -agentCurrentDir.x)) * agentRadius + SeperationRangeAddition / 2;

        float2 left1 = agentPos + leftEdgeOffset;
        float2 left2 = center2 + leftEdgeOffset;

        float2 right1 = agentPos + rightEdgeOffset;
        float2 right2 = center2 + rightEdgeOffset;


        float closestAgentDist = float.MaxValue;
        int closestAgentIndex = 0;
        float checkRange = agentRadius + 0.2f;
        for (int i = 0; i < AgentSpatialHashGrid.GetGridCount(); i++)
        {
            SpatialHashGridIterator iterator = AgentSpatialHashGrid.GetIterator(agentPos, checkRange, i);
            while (iterator.HasNext())
            {
                NativeSlice<AgentMovementData> agentsToCheck = iterator.GetNextRow(out int sliceStart);
                for (int j = 0; j < agentsToCheck.Length; j++)
                {
                    AgentMovementData mate = agentsToCheck[j];
                    float2 matePos = new float2(mate.Position.x, mate.Position.z);
                    if (j + sliceStart == agentIndex) { continue; }
                    if (mate.PathId == pathId) { continue; }
                    if (!HasStatusFlag(AgentStatus.Moving, mate.Status)) { continue; }
                    if (math.dot(agentCurrentDir, matePos - agentPos) < 0) { continue; }
                    if (math.dot(agentCurrentDir, mate.CurrentDirection) > 0) { continue; }
                    float desiredDistance = mate.Radius + checkRange;
                    float mateDistance = math.distance(agentPos, matePos);
                    float overlapping = desiredDistance - mateDistance;
                    if (overlapping <= 0) { continue; }

                    bool right = Intersects(right1, right2, matePos, mate.Radius + SeperationRangeAddition / 2);
                    bool left = Intersects(left1, left2, matePos, mate.Radius + SeperationRangeAddition / 2);
                    bool center = Intersects(center1, center2, matePos, mate.Radius + SeperationRangeAddition / 2);

                    if (right || left || center)
                    {
                        if (mateDistance < closestAgentDist)
                        {
                            closestAgentDist = mateDistance;
                            closestAgentIndex = j + sliceStart;
                        }
                    }

                }
            }
        }
        if(closestAgentDist == float.MaxValue) { avoidance = 0; return 0; }
        AgentMovementData picked = AgentSpatialHashGrid.RawAgentMovementDataArray[closestAgentIndex];
        float2 pickedPos = new float2(picked.Position.x, picked.Position.z);
        float2 pickeDir = pickedPos - agentPos;
        float2 pickedToAgent = agentPos - pickedPos;

        //is picked avoiding left
        if(math.dot(pickedToAgent, new float2(-picked.CurrentDirection.y, picked.CurrentDirection.x)) < 0)
        {
            float2 avoidanceDirection = math.normalizesafe(new float2(-pickeDir.y, pickeDir.x));
            float2 steering = avoidanceDirection - agentCurrentDir;
            float steeringMaxLen = math.length(steering);
            steering = math.select(steering / steeringMaxLen * MovingAvoidanceRangeAddition, steering, MovingAvoidanceRangeAddition > steeringMaxLen);
            agentCurrentDir += steering;
            avoidance = math.dot(AgentSpatialHashGrid.RawAgentMovementDataArray[agentIndex].DesiredDirection, new float2(-agentCurrentDir.y, agentCurrentDir.x)) < 0 ? MovingAvoidanceStatus.L : MovingAvoidanceStatus.R;
            return agentCurrentDir;
        }
        else
        {
            float2 avoidanceDirection = math.normalizesafe(new float2(pickeDir.y, -pickeDir.x));
            float2 steering = avoidanceDirection - agentCurrentDir;
            float steeringMaxLen = math.length(steering);
            steering = math.select(steering / steeringMaxLen * MovingAvoidanceRangeAddition, steering, MovingAvoidanceRangeAddition > steeringMaxLen);
            agentCurrentDir += steering;
            avoidance = math.dot(AgentSpatialHashGrid.RawAgentMovementDataArray[agentIndex].DesiredDirection, new float2(-agentCurrentDir.y, agentCurrentDir.x)) < 0 ? MovingAvoidanceStatus.L : MovingAvoidanceStatus.R;
            return agentCurrentDir;
        }
    }
    bool IsNotAlignable(float2 agentPos, float2 alignedDir, int offset)
    {
        float2 linecastPoint2 = agentPos + (alignedDir * 2.5f);
        linecastPoint2.x = math.select(linecastPoint2.x, FieldMinXIncluding, linecastPoint2.x < FieldMinXIncluding);
        linecastPoint2.x = math.select(linecastPoint2.x, FieldMaxXExcluding - TileSize, linecastPoint2.x >= FieldMaxXExcluding);
        linecastPoint2.y = math.select(linecastPoint2.y, FieldMinYIncluding, linecastPoint2.y < FieldMinYIncluding);
        linecastPoint2.y = math.select(linecastPoint2.y, FieldMaxYExcluding - TileSize, linecastPoint2.y >= FieldMaxYExcluding);

        return LineCast(agentPos, linecastPoint2, offset);
    }
    bool LineCast(float2 point1, float2 point2, int offset)
    {
        UnsafeListReadOnly<byte> costs = CostFieldEachOffset[offset];
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