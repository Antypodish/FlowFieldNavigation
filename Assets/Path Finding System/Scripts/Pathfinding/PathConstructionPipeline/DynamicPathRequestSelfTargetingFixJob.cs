using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;


[BurstCompile]
public struct DynamicPathRequestSelfTargetingFixJob : IJob
{
    public NativeArray<int> AgentNewPathIndicies;
    [ReadOnly] public NativeArray<PathRequest> InitialPathRequests;
    public void Execute()
    {
        for (int i = 0; i < AgentNewPathIndicies.Length; i++)
        {
            int requestIndex = AgentNewPathIndicies[i];
            if (requestIndex == -1) { continue; }
            PathRequest request = InitialPathRequests[requestIndex];
            if (request.Type == DestinationType.DynamicDestination && request.TargetAgentIndex == i)
            {
                AgentNewPathIndicies[i] = -1;
            }
        }
    }
}
