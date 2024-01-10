using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct AgentStoppingJob : IJobParallelFor
{
    public NativeArray<bool> AgentDestinationReachStatus;
    [ReadOnly] public NativeArray<AgentMovementData> AgentMovementDataArray;
    [ReadOnly] public NativeArray<int> AgentFlockIndexArray;
    [ReadOnly] public NativeArray<int> FlockStoppedAgentCountArray;
    [ReadOnly] public NativeArray<int> NormalToHashed;
    public void Execute(int index)
    {

        bool isDestinationReached = AgentDestinationReachStatus[index];
        int hashedIndex = NormalToHashed[index];
        if (isDestinationReached) { return; }
        int agentFlockIndex = AgentFlockIndexArray[index];
        int flockStoppedAgentCount = FlockStoppedAgentCountArray[agentFlockIndex];

        const float agentRadius = 0.5f;
        float occupiedArea = flockStoppedAgentCount * math.PI * agentRadius * agentRadius;
        float occupiedAreaRadius = math.sqrt(occupiedArea / math.PI);
        float requiredDistance = occupiedAreaRadius + agentRadius;
        float requiredDistanceSquared = requiredDistance * requiredDistance;
        AgentMovementData agentData = AgentMovementDataArray[hashedIndex];
        float2 agentPos = new float2(agentData.Position.x, agentData.Position.z);
        float distanceToDestinationSquared = math.distancesq(agentPos, agentData.Destination);
        if (distanceToDestinationSquared <= requiredDistanceSquared)
        {
            AgentDestinationReachStatus[index] = true;
        }

        //!!!ATTENTION
        //Only works with size 0.5
        //(DONE)I also need to update agent status by clearing "moving" bit
        //(DONE)AgentDestinationReachStatus bits should be cleared if started to a new path
    }
}
