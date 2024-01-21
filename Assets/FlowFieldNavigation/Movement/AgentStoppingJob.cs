using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
internal struct AgentStoppingJob : IJobParallelFor
{
    internal NativeArray<bool> AgentDestinationReachStatus;
    [ReadOnly] internal NativeArray<AgentMovementData> AgentMovementDataArray;
    [ReadOnly] internal NativeArray<float> PathDestinationReachRanges;
    [ReadOnly] internal NativeArray<int> NormalToHashed;
    [ReadOnly] internal NativeArray<int> AgentCurPathIndicies;
    [ReadOnly] internal NativeArray<bool> PathAgentStopFlags;
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
