using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
internal struct AgentsWithNewPathJob : IJob
{
    [ReadOnly] public NativeArray<int> AgentNewPathIndicies;
    [WriteOnly] public NativeList<int> AgentsWithNewPath;
    public void Execute()
    {
        for(int i = 0; i <AgentNewPathIndicies.Length; i++)
        {
            if (AgentNewPathIndicies[i] != -1)
            {
                AgentsWithNewPath.Add(i);
            }
        }
    }
}
