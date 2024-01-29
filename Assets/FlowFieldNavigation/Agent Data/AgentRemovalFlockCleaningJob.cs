using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;
using Unity.Mathematics;

[BurstCompile]
internal struct AgentRemovalFlockCleaningJob : IJob
{
    [ReadOnly] internal NativeArray<int> RemovedAgentIndicies;
    [ReadOnly] internal NativeArray<int> AgentFlockIndicies;
    internal NativeList<int> UnusedFlockIndicies;
    internal NativeArray<Flock> FlockList;
    public void Execute()
    {
        for(int i = 0; i < RemovedAgentIndicies.Length; i++)
        {
            int agentIndex = RemovedAgentIndicies[i];
            int agentFlockIndex = AgentFlockIndicies[agentIndex];
            if(agentFlockIndex == 0) { continue; }

            Flock flockPointed = FlockList[agentFlockIndex];
            flockPointed.AgentCount--;
            FlockList[agentFlockIndex] = flockPointed;
            if(flockPointed.AgentCount <= 0) { UnusedFlockIndicies.Add(agentFlockIndex); }
        }
    }
}