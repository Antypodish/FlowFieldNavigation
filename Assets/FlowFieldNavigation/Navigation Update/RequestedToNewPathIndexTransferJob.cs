using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;

[BurstCompile]
public struct RequestedToNewPathIndexTransferJob : IJob
{
    public NativeArray<int> AgentRequestedPathIndicies;
    public NativeArray<int> AgentNewPathIndicies;
    public void Execute()
    {
        int length = AgentRequestedPathIndicies.Length;
        for (int i = 0; i < length; i++)
        {
            AgentNewPathIndicies[i] = AgentRequestedPathIndicies[i];
            AgentRequestedPathIndicies[i] = -1;
        }
    }
}
