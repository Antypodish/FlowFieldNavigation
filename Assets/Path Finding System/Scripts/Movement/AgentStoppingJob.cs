using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct AgentStoppingJob : IJobParallelFor
{
    public NativeArray<bool> AgentDestinationReachStatus;
    [ReadOnly] public NativeArray<AgentMovementData> AgentMovementDataArray;
    [ReadOnly] public NativeArray<float> PathDestinationReachRanges;
    [ReadOnly] public NativeArray<int> NormalToHashed;
    [ReadOnly] public NativeArray<int> AgentCurPathIndicies;
    [ReadOnly] public NativeArray<bool> PathAgentStopFlags;
    public void Execute(int index)
    {

        bool isDestinationReached = AgentDestinationReachStatus[index];
        if (isDestinationReached) { return; }
        int agentPathIndex = AgentCurPathIndicies[index];
        if(agentPathIndex == -1) { return; }
        if (!PathAgentStopFlags[agentPathIndex]) { return; }
        float pathDestinationReachRange = PathDestinationReachRanges[agentPathIndex];
        int hashedIndex = NormalToHashed[index];
        AgentMovementData agentData = AgentMovementDataArray[hashedIndex];
        float2 agentPos = new float2(agentData.Position.x, agentData.Position.z);
        float distanceToDestination = math.distance(agentPos, agentData.Destination);
        if (distanceToDestination <= pathDestinationReachRange + agentData.Radius)
        {
            AgentDestinationReachStatus[index] = true;
        }
    }
}
