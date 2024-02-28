using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace FlowFieldNavigation
{
    [BurstCompile]
    internal struct AgentRemovalPathRequestCleanupJob : IJob
    {
        [ReadOnly] internal NativeArray<int> AgentRemovalMarks;
        internal NativeList<int> AgentNewPathIndicies;
        internal NativeArray<PathRequest> PathRequests;
        public void Execute()
        {
            for (int pathRequestIndex = 0; pathRequestIndex < PathRequests.Length; pathRequestIndex++)
            {
                PathRequest request = PathRequests[pathRequestIndex];
                if (request.Type != DestinationType.DynamicDestination) { continue; }
                int targetAgent = request.TargetAgentIndex;
                int targetAgentRemovalMark = AgentRemovalMarks[targetAgent];
                if (targetAgentRemovalMark == -1) { continue; }
                if (targetAgentRemovalMark == -2)
                {
                    request.Type = DestinationType.None;
                    PathRequests[pathRequestIndex] = request;
                    continue;
                }
                if (targetAgentRemovalMark >= 0)
                {
                    request.TargetAgentIndex = targetAgentRemovalMark;
                    PathRequests[pathRequestIndex] = request;
                    continue;
                }
            }

            for (int agentIndex = 0; agentIndex < AgentNewPathIndicies.Length; agentIndex++)
            {
                int requestedPathIndex = AgentNewPathIndicies[agentIndex];
                if (requestedPathIndex == -1) { continue; }
                PathRequest request = PathRequests[requestedPathIndex];
                if (request.Type == DestinationType.None) { AgentNewPathIndicies[agentIndex] = -1; }
            }
        }
    }


}
