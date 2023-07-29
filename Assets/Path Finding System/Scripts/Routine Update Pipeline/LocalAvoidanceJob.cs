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
            finalDirection = GetAlignedDirection(agentPos, index, finalDirection);
            finalDirection = GetSeperatedDirection(agentPos, index, finalDirection);
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
            if((mateData.Status & AgentStatus.Moving) != AgentStatus.Moving) { continue; }

            float2 matePos = new float2(mateData.Position.x, mateData.Position.z);
            float distance = math.distance(matePos, agentPos);

            if (distance > SeperationRadius * 2 * 1f) { continue; }

            float dot = math.dot(desiredDirection, matePos - agentPos);
            if (dot <= 0) { continue; }

            float overlapping = SeperationRadius * 2 - distance;
            totalSeperation += math.select(math.normalize(agentPos - matePos) * overlapping, 0, agentPos.Equals(matePos) || overlapping == 0);
        }
        if(totalSeperation.Equals(0)) { return desiredDirection; }
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

            float2 foreignAgentPosition = new float2(mateData.Position.x, mateData.Position.z);
            float distance = math.distance(foreignAgentPosition, agentPos);

            if (distance > SeperationRadius * 2) { continue; }

            float overlapping = SeperationRadius * 2 - distance;
            totalSeperation += math.select(math.normalize(agentPos - foreignAgentPosition) * overlapping, 0, agentPos.Equals(foreignAgentPosition) || overlapping == 0);
        }
        if (totalSeperation.Equals(0)) { return desiredDirection; }
        float2 seperationDirection = math.normalize(totalSeperation);
        float2 steering = (seperationDirection - desiredDirection) * SeperationMultiplier;
        float2 newDirection = math.select(math.normalize(desiredDirection + steering), 0, (desiredDirection + steering).Equals(0));
        //float2 directionToReturn = math.select(math.normalize(newDirection - desiredDirection), 0f, newDirection.Equals(desiredDirection));
        return newDirection;
    }
    float2 GetAlignedDirection(float2 agentPos, int agentIndex, float2 desiredDirection)
    {
        float2 totalHeading = 0;
        int count = 0;
        for (int i = 0; i < AgentMovementDataArray.Length; i++)
        {
            AgentMovementData mateData = AgentMovementDataArray[i];

            if (i == agentIndex) { continue; }
            if (mateData.Flow.Equals(0)) { continue; }
            
            float2 matePos = new float2(mateData.Position.x, mateData.Position.z);

            float dot = math.dot(desiredDirection, matePos - agentPos);
            //if (dot <= 0) { continue; }

            float distance = math.distance(matePos, agentPos);

            if (distance > AlignmentRadius * 2) { continue; }

            float overlapping = AlignmentRadius * 2 - distance;
            totalHeading += mateData.Flow * overlapping;
            count++;
        }

        if (totalHeading.Equals(0)) { return desiredDirection; }
        float2 averageHeading = math.normalize(totalHeading);
        float2 steering = (averageHeading - desiredDirection) * AlignmentMultiplier;
        float2 newDirection = math.select(math.normalize(desiredDirection + steering), 0, (desiredDirection + steering).Equals(0));
        return newDirection;
    }
}
