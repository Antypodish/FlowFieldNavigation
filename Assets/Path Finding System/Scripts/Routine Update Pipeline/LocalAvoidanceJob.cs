
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

[BurstCompile]
public struct LocalAvoidanceJob : IJobParallelFor
{
    public float SeekMultiplier;
    public float AlignmentMultiplier;
    public float SeperationMultiplier;
    public float SeperationRangeAddition;
    public float AlignmentRangeAddition;
    public float MaxSeperationMagnitude;
    [ReadOnly] public NativeArray<AgentMovementData> AgentMovementDataArray;
    [WriteOnly] public NativeArray<RoutineResult> RoutineResultArray;

    public void Execute(int index)
    {
        AgentMovementData agent = AgentMovementDataArray[index];
        RoutineResult newRoutineResult = new RoutineResult()
        {
            NewAvoidance = agent.Avoidance,
            NewSplitInfo = agent.SplitInfo,
            NewSplitInterval = agent.SplitInterval,
        };

        float2 agentPos = new float2(agent.Position.x, agent.Position.z);
        float2 newDirectionToSteer = agent.DesiredDirection;

        if (HasStatusFlag(AgentStatus.HoldGround, agent.Status)) { return; }
        //GET AVOIDANCE STATUS
        if (newRoutineResult.NewAvoidance == 0)
        {
            newRoutineResult.NewAvoidance = GetAvoidanceStatus(agentPos, agent.DesiredDirection, agent.Radius, index);
            if (newRoutineResult.NewAvoidance != 0)
            {
                newRoutineResult.NewSplitInterval = 50;
            }
        }

        //CHECK IF DESIRED DIRECTION IS FREE
        if (newRoutineResult.NewAvoidance != 0 && math.dot(agent.DesiredDirection, agent.CurrentDirection) > 0)
        {
            newRoutineResult.NewAvoidance = IsDirectionFree(agentPos, agent.DesiredDirection, index, agent.Radius, agent.Radius + 1f) ? 0 : newRoutineResult.NewAvoidance;
        }

        //GET AVIODANCE DIRECTION
        if (newRoutineResult.NewAvoidance != 0)
        {
            float2 avoidanceDirection = GetAvoidanceDirection(agentPos, agent.CurrentDirection, agent.Radius, index, agent.Radius + 1f, newRoutineResult.NewAvoidance);
            newDirectionToSteer = avoidanceDirection;
            if (avoidanceDirection.Equals(0))
            {
                newRoutineResult.NewAvoidance = 0;
            }
        }

        //GET SEPERATİON
        float2 seperation = GetSeperation(agentPos, agent.CurrentDirection, agent.Radius, index, newRoutineResult.NewAvoidance);

        //GET ALIGNMENT
        if (newRoutineResult.NewAvoidance == 0)
        {
            float2 alignment = GetAlignment(agentPos, agent.DesiredDirection, index, agent.PathId, agent.Radius);
            newDirectionToSteer += alignment;
        }

        //GET SEEK
        float2 seek = GetSeek(agent.CurrentDirection, newDirectionToSteer);

        //COMBINE FORCES
        if (HasStatusFlag(AgentStatus.Moving, agent.Status))
        {
            newRoutineResult.NewDirection = agent.CurrentDirection + seek;
            newRoutineResult.NewSeperation = seperation;
        }
        else
        {
            newRoutineResult.NewDirection = (agent.Status & AgentStatus.HoldGround) == AgentStatus.HoldGround ? 0f : GetStoppedSeperationForce(agentPos, agent.Radius, index);
        }
        RoutineResultArray[index] = newRoutineResult;
    }

    float2 GetSeek(float2 currentDirection, float2 desiredDirection)
    {
        float2 steeringToSeek = desiredDirection - currentDirection;
        float steeringToSeekLen = math.length(steeringToSeek);
        return math.select(steeringToSeek / steeringToSeekLen, 0f, steeringToSeekLen == 0) * math.select(SeekMultiplier, steeringToSeekLen, steeringToSeekLen < SeekMultiplier);
    }
    float2 GetAlignment(float2 agentPos, float2 desiredDirection, int agentIndex, int pahtId, float radius)
    {
        float2 totalHeading = 0;
        int alignedAgentCount = 0;

        float2 toalCurrentHeading = 0;
        bool avoiding = false;
        for(int i = 0; i < AgentMovementDataArray.Length; i++)
        {
            AgentMovementData mate = AgentMovementDataArray[i];
            float2 matePos = new float2(mate.Position.x, mate.Position.z);
            float distance = math.distance(agentPos, matePos);
            float desiredDistance = mate.Radius + radius + AlignmentRangeAddition;
            float overlapping = desiredDistance - distance;
            
            if (i == agentIndex) { continue; }
            if (math.dot(matePos - agentPos, desiredDirection) < 0) { continue; }
            if(!HasStatusFlag(AgentStatus.Moving, mate.Status)) { continue; }
            if (mate.PathId != pahtId) { continue; }
            if (overlapping <= 0) { continue; }

            totalHeading += mate.DesiredDirection;
            toalCurrentHeading += math.normalizesafe(mate.CurrentDirection);
            alignedAgentCount++;
            if(mate.Avoidance != 0) { avoiding = true; }
        }
        if (avoiding)
        {
            return math.select((toalCurrentHeading / alignedAgentCount - desiredDirection) * AlignmentMultiplier, 0, alignedAgentCount == 0);
        }
        return math.select(totalHeading / alignedAgentCount - desiredDirection, 0, alignedAgentCount == 0);
    }
    float2 GetSeperation(float2 agentPos, float2 desiredDirection, float agentRadius, int agentIndex, AvoidanceStatus agentAvoidance)
    {
        float2 totalSeperation = 0;
        int seperationCount = 0;

        if(agentAvoidance == 0)
        {
            for (int i = 0; i < AgentMovementDataArray.Length; i++)
            {
                AgentMovementData mate = AgentMovementDataArray[i];
                float2 matePos = new float2(mate.Position.x, mate.Position.z);
                float distance = math.distance(matePos, agentPos);
                float desiredRange = mate.Radius + agentRadius + SeperationRangeAddition;
                float overlapping = desiredRange - distance;

                if (overlapping <= 0) { continue; }
                if (!HasStatusFlag(AgentStatus.Moving, mate.Status)) { continue; }
                if (math.dot(desiredDirection, matePos - agentPos) < 0) { continue; }

                float2 seperationForce = math.normalizesafe(agentPos - matePos) * overlapping;
                seperationForce = math.select(seperationForce, new float2(i, 1), agentPos.Equals(matePos) && agentIndex < i);

                totalSeperation += seperationForce;
                seperationCount++;
            }
        }
        else
        {
            for (int i = 0; i < AgentMovementDataArray.Length; i++)
            {
                AgentMovementData mate = AgentMovementDataArray[i];
                float2 matePos = new float2(mate.Position.x, mate.Position.z);
                float distance = math.distance(matePos, agentPos);
                float desiredRange = mate.Radius + agentRadius + SeperationRangeAddition;
                float overlapping = desiredRange - distance;

                if (overlapping <= 0) { continue; }
                if (!HasStatusFlag(AgentStatus.Moving, mate.Status)) { continue; }
                if(mate.Avoidance == 0) { continue; }
                if (math.dot(desiredDirection, matePos - agentPos) < 0) { continue; }

                float2 seperationForce = math.normalizesafe(agentPos - matePos) * overlapping;
                seperationForce = math.select(seperationForce, new float2(i, 1), agentPos.Equals(matePos) && agentIndex < i);

                totalSeperation += seperationForce;
                seperationCount++;
            }
        }

        if(seperationCount == 0) { return 0; }
        totalSeperation /= seperationCount;
        return totalSeperation * SeperationMultiplier;
    }
    float2 GetStoppedSeperationForce(float2 agentPos, float agentRadius, int agentIndex)
    {
        float2 totalSeperation = 0;
        for (int i = 0; i < AgentMovementDataArray.Length; i++)
        {
            AgentMovementData mateData = AgentMovementDataArray[i];
            if (i == agentIndex) { continue; }

            float2 matePos = new float2(mateData.Position.x, mateData.Position.z);
            float distance = math.distance(matePos, agentPos);

            float seperationRadius = (agentRadius + mateData.Radius) + SeperationRangeAddition;
            if (distance > seperationRadius) { continue; }

            float overlapping = seperationRadius - distance;
            float multiplier = overlapping;
            float2 seperation = agentPos - matePos;
            seperation = math.select(seperation, new float2(i, 1), agentPos.Equals(matePos) && agentIndex < i);
            seperation = math.normalizesafe(seperation) * multiplier;
            totalSeperation += seperation;
        }
        return totalSeperation;
    }
    AvoidanceStatus GetAvoidanceStatus(float2 agentPos, float2 desiredDirection, float agentRadius, int agentIndex)
    {
        NativeArray<AgentMovementData> agentMovementDataArray = AgentMovementDataArray;
        for (int i = 0; i < agentMovementDataArray.Length; i++)
        {
            AgentMovementData mateData = AgentMovementDataArray[i];
            if (i == agentIndex) { continue; }
            if ((mateData.Status & AgentStatus.HoldGround) != AgentStatus.HoldGround) { continue; }

            float2 matePos = new float2(mateData.Position.x, mateData.Position.z);
            float distance = math.distance(matePos, agentPos);

            float obstacleDetectionRange = (agentRadius + mateData.Radius) + SeperationRangeAddition + 0.1f;

            if (distance > obstacleDetectionRange) { continue; }

            float dot = math.dot(desiredDirection, matePos - agentPos);
            if (dot <= 0f) { continue; }

            return DetermineAvoidance();
        }
        return 0;

        AvoidanceStatus DetermineAvoidance()
        {
            float2 totalLeftAvoiance = 0;
            float2 totalRightAvoidance = 0;
            for (int i = 0; i < agentMovementDataArray.Length; i++)
            {
                AgentMovementData mateData = agentMovementDataArray[i];
                if (i == agentIndex) { continue; }
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
            totalLeftAvoiance = math.normalizesafe(totalLeftAvoiance);
            totalRightAvoidance = math.normalizesafe(totalRightAvoidance);
            float leftDot = math.dot(totalLeftAvoiance, desiredDirection);
            float rightDot = math.dot(totalRightAvoidance, desiredDirection);
            return leftDot > rightDot ? AvoidanceStatus.L : AvoidanceStatus.R;
        }
    }
    float2 GetAvoidanceDirection(float2 agentPos, float2 currentDirection, float agentRadius, int agentIndex, float maxCheckRange, AvoidanceStatus agentAvoidance)
    {
        float2 closestObstaclePos = 0;
        float closestObstacleRadius = 0;
        float closesObstacleDotProduct = float.MinValue;
        for (int i = 0; i < AgentMovementDataArray.Length; i++)
        {
            AgentMovementData mateData = AgentMovementDataArray[i];
            if (i == agentIndex) { continue; }
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

        for(int i = 0; i < AgentMovementDataArray.Length; i++)
        {
            AgentMovementData mate = AgentMovementDataArray[i];
            if (agentIndex == i) { continue; }
            if(!HasStatusFlag(AgentStatus.HoldGround, mate.Status)) { continue; }
            float3 matePos3 = mate.Position;
            float2 matePos2 = new float2(matePos3.x, matePos3.z);
            if(math.distance(agentPos, matePos2) > checkRange + mate.Radius) { continue; }
            float2 dirFromLeft = matePos2 - leftLinep1;
            float2 dirFromRight = matePos2 - rightLinep1;
            bool isRightOfLeft = math.dot(leftLinep2 - leftLinep1, new float2(dirFromLeft.y, -dirFromLeft.x)) < 0;
            bool isLeftOfRight = math.dot(rightLinep2 - rightLinep1, new float2(-dirFromRight.y, dirFromRight.x)) < 0;
            if (Intersects(leftLinep1, leftLinep2, matePos2, mate.Radius) || Intersects(rightLinep1, rightLinep2, matePos2, mate.Radius) || (isLeftOfRight && isRightOfLeft && math.dot(direction, matePos2 - agentPos) >= 0))
            {
                return false;
            }
        }
        return true;

        
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

                if(intersectionPoint.x <= rh.x && intersectionPoint.x >= lh.x)
                {
                    return math.distance(intersectionPoint, matePos) < mateRadius;
                }
                else
                {

                }
                return math.distance(matePos, lh) < mateRadius || math.distance(matePos, rh) < mateRadius;
            }
        }
    }
    bool HasStatusFlag(AgentStatus flag, AgentStatus agentStatus)
    {
        return (agentStatus & flag) == flag;
    }
}