using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.VisualScripting;

[BurstCompile]
public struct LocalAvoidanceJob : IJobParallelFor
{
    public float SeperationRadius;
    public float SeperationMultiplier;
    [ReadOnly] public NativeArray<AgentMovementData> AgentMovementData;
    public NativeArray<float2> AgentDirections;

    public void Execute(int index)
    {
        AgentMovementData agentData = AgentMovementData[index];
        float2 agentPos = new float2(agentData.Position.x, agentData.Position.z);
        float2 totalSeperationDirection = 0;
        bool isAgentMoving = (agentData.Status & AgentStatus.Moving) == AgentStatus.Moving;
        int directionCount = 0;
        for(int i = 0; i< AgentMovementData.Length; i++)
        {
            if(i == index) { continue; }
            AgentMovementData mateData = AgentMovementData[i];
            float2 matePos = new float2(mateData.Position.x, mateData.Position.z);

            //IF AGENT IS MOVING BUT MATE IS NOT
            if((mateData.Status & AgentStatus.Moving) != AgentStatus.Moving && isAgentMoving) { continue; }
            else if(!isAgentMoving) //IF AGENT IS NOT MOVING
            {
                float distance = math.distance(matePos, agentPos);
                if (distance <= SeperationRadius * 2)
                {
                    bool equalPos = Equals(matePos, agentPos);
                    if (equalPos && i > index)
                    {
                        totalSeperationDirection += new float2(1, 0);
                        directionCount++;
                    }
                    else if(!equalPos)
                    {
                        totalSeperationDirection += math.normalize(agentPos - matePos);
                        directionCount++;
                    }
                    
                }
            }
            else //IF AGENT AND MATE ARE MOVING
            {
                //IS IN FRONT
                float2 agentFlow = agentData.Flow;
                float2 mateDir = math.normalize(matePos - agentPos);
                if(math.dot(agentFlow, mateDir) < 0) { continue; }

                float distance = math.distance(matePos, agentPos);
                if (distance <= SeperationRadius * 2)
                {
                    totalSeperationDirection += math.select(math.normalize(agentPos - matePos), new float2(1, 0), Equals(matePos, agentPos) && i > index);
                    directionCount++;
                }
            }
        }
        float2 averageSeperationDirection = totalSeperationDirection / directionCount;
        if (directionCount == 0 || Equals(totalSeperationDirection, 0) || SeperationMultiplier == 0 || Equals(averageSeperationDirection, agentData.Flow)) { AgentDirections[index] = agentData.Flow; return; }
        float2 resultinSeperationForce = math.normalize(averageSeperationDirection - agentData.Flow) * SeperationMultiplier;
        float2 newDirection = agentData.Flow + resultinSeperationForce;
        AgentDirections[index] = math.normalize(newDirection);
    }
    bool Equals(float3 f1, float3 f2)
    {
        return f1.x == f2.x && f1.y == f2.y && f1.z == f2.z;
    }
    bool Equals(float2 f1, float2 f2)
    {
        return f1.x == f2.x && f1.y == f2.y;
    }
}
