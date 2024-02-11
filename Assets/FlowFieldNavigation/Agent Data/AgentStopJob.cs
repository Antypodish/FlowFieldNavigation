using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;

[BurstCompile]
internal struct AgentStopJob : IJob
{
    [ReadOnly] internal NativeArray<int> AgentIndiciesToStop;
    internal NativeArray<AgentData> AgentDataArray;

    public void Execute()
    {
        for (int i = 0; i < AgentIndiciesToStop.Length; i++)
        {
            int agentIndex = AgentIndiciesToStop[i];
            AgentData agentData = AgentDataArray[agentIndex];
            agentData.Status = 0;
            AgentDataArray[agentIndex] = agentData;
        }
    }
}