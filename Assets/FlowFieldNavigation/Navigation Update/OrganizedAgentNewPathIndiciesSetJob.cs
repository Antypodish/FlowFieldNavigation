using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;

[BurstCompile]
internal struct OrganizedAgentNewPathIndiciesSetJob : IJob
{
    internal NativeArray<int> AgentNewPathIndicies;
    [ReadOnly] internal NativeArray<FinalPathRequest> RequestedPaths;
    public void Execute()
    {
        for (int i = 0; i < AgentNewPathIndicies.Length; i++)
        {
            int pathReqIndex = AgentNewPathIndicies[i];
            if (pathReqIndex < 0) { continue; }
            FinalPathRequest path = RequestedPaths[pathReqIndex];
            if (!path.IsValid()) { continue; }
            AgentNewPathIndicies[i] = path.PathIndex;
        }
    }
}