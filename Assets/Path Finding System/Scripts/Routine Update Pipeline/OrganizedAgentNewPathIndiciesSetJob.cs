using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;

[BurstCompile]
public struct OrganizedAgentNewPathIndiciesSetJob : IJob
{
    public NativeArray<int> AgentNewPathIndicies;
    [ReadOnly] public NativeArray<PathRequest> CurrentRequestedPaths;
    public void Execute()
    {
        for (int i = 0; i < AgentNewPathIndicies.Length; i++)
        {
            int pathReqIndex = AgentNewPathIndicies[i];
            if (pathReqIndex < 0) { continue; }
            PathRequest path = CurrentRequestedPaths[pathReqIndex];
            if (!path.IsValid()) { continue; }
            AgentNewPathIndicies[i] = path.PathIndex;
        }
    }
}