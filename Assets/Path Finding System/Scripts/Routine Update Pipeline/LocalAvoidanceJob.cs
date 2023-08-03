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
    public float SeperationRadius;
    public float SeperationMultiplier;
    public float AlignmentRadius;
    public float AlignmentMultiplier;
    [ReadOnly] public NativeArray<AgentMovementData> AgentMovementDataArray;
    public NativeArray<float2> AgentDirections;

    public void Execute(int index)
    {
        AgentMovementData agentData = AgentMovementDataArray[index];
        float2 agentPos = new float2(agentData.Position.x, agentData.Position.z);
        float2 totalSeperationDirection = 0;
        bool isAgentMoving = (agentData.Status & AgentStatus.Moving) == AgentStatus.Moving;
        float2 finalDirection = agentData.Flow;
        if (isAgentMoving)
        {
            finalDirection = GetAlignedDirection(agentPos, index, finalDirection, agentData.waypoint, agentData.PathId);
            finalDirection = GetSeperationNew(agentPos, index, finalDirection);
            AgentDirections[index] = finalDirection;
        }
        else
        {
            AgentDirections[index] = GetPushingForce(agentPos, index, agentData.Flow);
        }
    }
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
    float2 GetSeperationNew(float2 agentPos, int agentIndex, float2 desiredDirection)
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
            float multiplier = overlapping * SeperationMultiplier;
            totalSeperation += math.select(math.normalize(agentPos - matePos) * multiplier, 0, agentPos.Equals(matePos) || overlapping == 0);
        }
        if (totalSeperation.Equals(0)) { return desiredDirection; }
        float2 newVelocity = desiredDirection + totalSeperation;
        if (math.dot(newVelocity, desiredDirection) < 0)
        {
            newVelocity = math.select(math.normalize(newVelocity), 0f, newVelocity.Equals(0f));
            float2 steering = (newVelocity - desiredDirection) * 0.5f;
            newVelocity = math.select(math.normalize(desiredDirection + steering), 0f, desiredDirection.Equals(-steering));
        }
        else
        {
            newVelocity = math.select(newVelocity, math.normalize(newVelocity), math.length(newVelocity) > 1);
        }
        return newVelocity;
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
        float2 steering = (seperationDirection - desiredDirection) * SeperationMultiplier;
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
            if(pathId != mateData.PathId) { continue; }

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
    }
}
