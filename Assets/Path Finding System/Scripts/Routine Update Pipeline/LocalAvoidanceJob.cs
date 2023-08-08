using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

[BurstCompile]
public struct LocalAvoidanceJob : IJobParallelFor
{
    public float MovingForeignFlockSeperationRangeMultiplier;
    public float SeperationRangeAddition;
    public float SeperationMultiplier;
    public float AlignmentRadiusMultiplier;
    public float AlignmentDecreaseStartDistance;
    [ReadOnly] public NativeArray<AgentMovementData> AgentMovementDataArray;
    public NativeArray<float2> AgentDirections;
    

    public void Execute(int index)
    {
        AgentMovementData agentData = AgentMovementDataArray[index];
        float2 agentPos = new float2(agentData.Position.x, agentData.Position.z);
        bool isAgentMoving = (agentData.Status & AgentStatus.Moving) == AgentStatus.Moving;
        float2 finalDirection = agentData.Flow;
        if (isAgentMoving)
        {
            if (agentData.waypoint.position.Equals(agentData.Destination))
            {
                finalDirection = GetAlignmentDirectionToDestination(agentPos, agentData.Radius, index, finalDirection, agentData.PathId, agentData.Destination);
            }
            else
            {
                finalDirection = GetAlignedDirectionDecreasing(agentPos, agentData.Radius, index, finalDirection, agentData.waypoint, agentData.PathId);
            }
            finalDirection = GetSeperationNew(agentPos, agentData.Radius, agentData.PathId, index, finalDirection);
            AgentDirections[index] = finalDirection;
        }
        else
        {
            AgentDirections[index] = GetPushinfForceNew(agentPos, agentData.Radius, index, agentData.Flow);
        }
    }
    
    float2 GetSeperationNew(float2 agentPos, float agentRadius, int pathId, int agentIndex, float2 desiredDirection)
    {
        float2 totalSeperation = 0;
        bool nonFlowMate = false;
        for (int i = 0; i < AgentMovementDataArray.Length; i++)
        {
            AgentMovementData mateData = AgentMovementDataArray[i];
            if (i == agentIndex) { continue; }
            if ((mateData.Status & AgentStatus.Moving) != AgentStatus.Moving) { continue; }
            float2 matePos = new float2(mateData.Position.x, mateData.Position.z);
            float distance = math.distance(matePos, agentPos);
            
            float seperationRadius = (agentRadius + mateData.Radius) + SeperationRangeAddition;
            if (mateData.PathId != pathId)
            {
                nonFlowMate = true;
                seperationRadius *= MovingForeignFlockSeperationRangeMultiplier;
            }
            if (distance > seperationRadius) { continue; }

            float dot = math.dot(desiredDirection, matePos - agentPos);
            if (dot <= 0) { continue; }

            float overlapping = seperationRadius - distance;
            float multiplier = overlapping * SeperationMultiplier;
            totalSeperation += math.select(math.normalize(agentPos - matePos) * multiplier, 0, agentPos.Equals(matePos) || overlapping == 0);
        }
        if (totalSeperation.Equals(0)) { return desiredDirection; }
        float2 newVelocity = desiredDirection + totalSeperation;
        if (!nonFlowMate && math.dot(newVelocity, desiredDirection) < 0)
        {

            float2 newDir = math.select(math.normalize(newVelocity), 0f, newVelocity.Equals(0));
            float2 perp1 = new float2(1, (-desiredDirection.x) / math.select(desiredDirection.y, 0.000001f , desiredDirection.y == 0));;
            float2 perp2 = new float2(-1, desiredDirection.x / math.select(desiredDirection.y, 0.000001f, desiredDirection.y == 0));
            perp1 = math.normalize(perp1);
            perp2 = math.normalize(perp2);
            float perp1Distance = math.distance(perp1, newDir);
            float perp2Distance = math.distance(perp2, newDir);
            newVelocity = math.select(perp2, perp1, perp1Distance < perp2Distance);
        }
        else
        {
            newVelocity = math.select(newVelocity, math.normalize(newVelocity), math.length(newVelocity) > 1);
        }
        return newVelocity;
    }
    float2 GetPushinfForceNew(float2 agentPos, float agentRadius, int agentIndex, float2 desiredDirection)
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
            float multiplier = overlapping * SeperationMultiplier;
            float2 push = math.select(math.normalize(agentPos - matePos) * multiplier, 0, overlapping == 0);
            push = math.select(push, math.normalize(new float2(i, 1)), agentPos.Equals(matePos));
            totalSeperation += push;
        }
        if (totalSeperation.Equals(0)) { return desiredDirection; }
        float2 newVelocity = desiredDirection + totalSeperation;
        newVelocity = math.select(newVelocity, math.normalize(newVelocity), math.length(newVelocity) > 1);
        return newVelocity;
    }

    float2 GetAlignedDirectionDecreasing(float2 agentPos, float agentRadius, int agentIndex, float2 desiredDirection, Waypoint agentWaypoint, int pathId)
    {
        float2 totalHeading = 0;
        for (int i = 0; i < AgentMovementDataArray.Length; i++)
        {
            AgentMovementData mateData = AgentMovementDataArray[i];
            float2 mateDirection = mateData.Flow;

            if (i == agentIndex) { continue; }
            if (mateDirection.Equals(0)) { continue; }
            if (!mateData.waypoint.position.Equals(agentWaypoint.position)) { continue; }
            if (pathId != mateData.PathId) { continue; }

            float2 matePos = new float2(mateData.Position.x, mateData.Position.z);

            float mateRelativeLocation = math.dot(desiredDirection, matePos - agentPos);
            float mateRelativeDirection = math.dot(desiredDirection, mateDirection);
            if (mateRelativeLocation <= 0) { continue; }
            if (mateRelativeDirection <= 0) { continue; }

            float distance = math.distance(matePos, agentPos);
            float alignmentRadius = (agentRadius + mateData.Radius) * AlignmentRadiusMultiplier;
            if (distance > alignmentRadius) { continue; }

            totalHeading += mateDirection;
        }

        if (totalHeading.Equals(0)) { return desiredDirection; }
        float2 averageHeading = math.normalize(totalHeading);
        float waypointDistance = math.distance(agentPos, agentWaypoint.position);
        float multiplier = math.select(waypointDistance / AlignmentDecreaseStartDistance, 1, waypointDistance > AlignmentDecreaseStartDistance);
        float2 steering = (averageHeading - desiredDirection) * multiplier;
        float2 newDirection = math.select(math.normalize(desiredDirection + steering), 0, (desiredDirection + steering).Equals(0));
        return newDirection;
    }
    float2 GetAlignmentDirectionToDestination(float2 agentPos, float agentRadius, int agentIndex, float2 desiredDirection, int pathId, float2 destination)
    {
        float2 totalHeading = 0;
        for (int i = 0; i < AgentMovementDataArray.Length; i++)
        {
            AgentMovementData mateData = AgentMovementDataArray[i];
            float2 mateDirection = mateData.Flow;

            if (i == agentIndex) { continue; }
            if (mateDirection.Equals(0)) { continue; }
            if (!mateData.waypoint.position.Equals(destination)) { continue; }
            if (pathId != mateData.PathId) { continue; }

            float mateRelativeDirection = math.dot(desiredDirection, mateDirection);
            if (mateRelativeDirection <= 0) { continue; }

            float2 matePos = new float2(mateData.Position.x, mateData.Position.z);
            float distance = math.distance(matePos, agentPos);
            float alignmentRadius = (agentRadius + mateData.Radius) * AlignmentRadiusMultiplier;
            if (distance > alignmentRadius) { continue; }
            totalHeading += mateDirection;
        }

        if (totalHeading.Equals(0)) { return desiredDirection; }
        float2 averageHeading = math.normalize(totalHeading);
        float2 newDirection = averageHeading;
        return newDirection;
    }
    float2 GetSeperationOld(float2 agentPos, float agentRadius, int pathId, int agentIndex, float2 desiredDirection)
    {
        float _flockmateSeperationRange = 1.2f;
        float _foreignSeperationRange = 1.6f;
        float _foreignSeperationMultiplier = 0.48f;
        float _flockmateSeperationMultiplier = 0.7f;

        float2 totalSeperation = 0;
        bool foreign = false;
        bool push = false;
        for (int i = 0; i < AgentMovementDataArray.Length; i++)
        {
            AgentMovementData mateData = AgentMovementDataArray[i];

            if (agentIndex == i) { continue; }

            if (pathId == mateData.PathId)
            {
                float2 foreignAgentPosition = new float2(mateData.Position.x, mateData.Position.z);
                float distance = math.distance(foreignAgentPosition, agentPos);

                if (distance > _flockmateSeperationRange) { continue; }

                float dot = math.dot(desiredDirection, foreignAgentPosition - agentPos);
                if (dot <= 0) { continue; }

                float overlapping = _flockmateSeperationRange - distance;
                totalSeperation += math.select(math.normalize(agentPos - foreignAgentPosition) * overlapping, 0f, agentPos.Equals(foreignAgentPosition));
            }
            else
            {
                float2 foreignAgentPosition = new float2(mateData.Position.x, mateData.Position.z);
                float distance = math.distance(foreignAgentPosition, agentPos);

                if (distance > _foreignSeperationRange) { continue; }

                float dot = math.dot(desiredDirection, foreignAgentPosition - agentPos);
                if (dot <= 0) { continue; }

                float overlapping = _foreignSeperationRange - distance;
                if (overlapping > 0.6) { push = true; }
                totalSeperation += math.select(math.normalize(agentPos - foreignAgentPosition) * overlapping, 0f, agentPos.Equals(foreignAgentPosition));
                foreign = true;
            }
        }
        if (totalSeperation.Equals(0f)) { return desiredDirection; }
        float2 seperationDirection = math.normalize(totalSeperation);
        _foreignSeperationMultiplier = math.select(_foreignSeperationMultiplier, 0.9f, push);
        float2 steering = (seperationDirection - desiredDirection) * math.select(_flockmateSeperationMultiplier, _foreignSeperationMultiplier, foreign);
        float2 newDirection = math.select(math.normalize(desiredDirection + steering), 0f, desiredDirection.Equals(-steering));
        return newDirection;
    }
    /*
    //LEGACY ALGORITHMS
    float2 GetSeperatedDirection(float2 agentPos, int agentIndex, float2 desiredDirection)
    {
        float2 totalSeperation = 0;
        for (int i = 0; i < AgentMovementDataArray.Length; i++)
        {
            AgentMovementData mateData = AgentMovementDataArray[i];
            if (i == agentIndex) { continue; }
            if ((mateData.Status & AgentStatus.Moving) != AgentStatus.Moving) { continue; }

            float2 matePos = new float2(mateData.Position.x, mateData.Position.z);
            float distance = math.distance(matePos, agentPos);

            if (distance > SeperationRadius * 2 * 1f) { continue; }

            float dot = math.dot(desiredDirection, matePos - agentPos);
            if (dot <= 0) { continue; }

            float overlapping = SeperationRadius * 2 - distance;
            totalSeperation += math.select(math.normalize(agentPos - matePos) * overlapping, 0, agentPos.Equals(matePos) || overlapping == 0);
        }
        if (totalSeperation.Equals(0)) { return desiredDirection; }
        float2 seperationDirection = math.normalize(totalSeperation);
        float2 steering = (seperationDirection - desiredDirection) * SeperationMultiplier;
        float2 newDirection = math.select(math.normalize(desiredDirection + steering), 0, (desiredDirection + steering).Equals(0));
        return newDirection;
    }
    float2 GetPushingForce(float2 agentPos, int agentIndex, float2 desiredDirection)
    {
        float2 totalSeperation = 0;
        for (int i = 0; i < AgentMovementDataArray.Length; i++)
        {
            AgentMovementData mateData = AgentMovementDataArray[i];

            if (i == agentIndex) { continue; }

            float2 matePos = new float2(mateData.Position.x, mateData.Position.z);
            float distance = math.distance(matePos, agentPos);

            if (distance > SeperationRadius * 2) { continue; }

            float overlapping = SeperationRadius * 2 - distance;

            float2 push = math.select(math.normalize(agentPos - matePos) * overlapping, 0, overlapping == 0);
            push = math.select(push, math.normalize(new float2(i, 1)), agentPos.Equals(matePos));
            totalSeperation += push;
        }
        if (totalSeperation.Equals(0)) { return desiredDirection; }
        float2 seperationDirection = math.normalize(totalSeperation);
        float2 steering = (seperationDirection - desiredDirection) * 0.5f;
        float2 newDirection = math.select(math.normalize(desiredDirection + steering), 0, (desiredDirection + steering).Equals(0));
        return newDirection;
    }
    float2 GetAlignedDirection(float2 agentPos, int agentIndex, float2 desiredDirection, Waypoint agentWaypoint, int pathId)
    {
        float2 totalHeading = 0;
        int count = 0;
        for (int i = 0; i < AgentMovementDataArray.Length; i++)
        {
            AgentMovementData mateData = AgentMovementDataArray[i];
            float2 mateDirection = mateData.Flow;

            if (i == agentIndex) { continue; }
            if (mateDirection.Equals(0)) { continue; }
            if (!mateData.waypoint.position.Equals(agentWaypoint.position)) { continue; }
            if (pathId != mateData.PathId) { continue; }

            float2 matePos = new float2(mateData.Position.x, mateData.Position.z);

            float mateRelativeLocation = math.dot(desiredDirection, matePos - agentPos);
            float mateRelativeDirection = math.dot(desiredDirection, mateDirection);
            if (mateRelativeLocation <= 0) { continue; }
            if (mateRelativeDirection <= 0) { continue; }

            float distance = math.distance(matePos, agentPos);

            if (distance > AlignmentRadius * 2) { continue; }

            float overlapping = AlignmentRadius * 2 - distance;
            totalHeading += mateDirection;
            count++;
        }

        if (totalHeading.Equals(0)) { return desiredDirection; }
        float2 averageHeading = math.normalize(totalHeading);
        float2 steering = (averageHeading - desiredDirection) * AlignmentMultiplier;
        float2 newDirection = math.select(math.normalize(desiredDirection + steering), 0, (desiredDirection + steering).Equals(0));
        return newDirection;
    }*/
}
