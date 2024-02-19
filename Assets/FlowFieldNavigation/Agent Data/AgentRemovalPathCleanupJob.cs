using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Jobs;

namespace FlowFieldNavigation
{
    [BurstCompile]
    internal struct AgentRemovalPathCleanupJob : IJob
    {
        [ReadOnly] internal NativeArray<int> AgentRemovalMarks;
        [ReadOnly] internal NativeArray<PathState> PathStates;
        internal NativeArray<AgentData> AgentDataArray;
        internal NativeArray<PathDestinationData> PathDestinationDataArray;
        internal NativeArray<int> PathSubscriberCounts;
        internal NativeArray<int> AgentCurPathIndicies;
        public void Execute()
        {
            for (int pathIndex = 0; pathIndex < PathStates.Length; pathIndex++)
            {
                if (PathStates[pathIndex] == PathState.Removed) { continue; }
                PathDestinationData destinationData = PathDestinationDataArray[pathIndex];
                if (destinationData.DestinationType != DestinationType.DynamicDestination) { continue; }
                int targetAgentIndex = destinationData.TargetAgentIndex;
                int targetAgentRemovalMark = AgentRemovalMarks[targetAgentIndex];
                if (targetAgentRemovalMark == -1) { continue; }
                if (targetAgentRemovalMark >= 0)
                {
                    destinationData.TargetAgentIndex = targetAgentRemovalMark;
                    PathDestinationDataArray[pathIndex] = destinationData;
                    continue;
                }
                if (targetAgentRemovalMark == -2)
                {
                    PathSubscriberCounts[pathIndex] = 0;
                }
            }

            for (int agentIndex = 0; agentIndex < AgentCurPathIndicies.Length; agentIndex++)
            {
                int curPathIndex = AgentCurPathIndicies[agentIndex];
                if (curPathIndex == -1) { continue; }
                int curPathSubscriberCount = PathSubscriberCounts[curPathIndex];
                if (curPathSubscriberCount == 0)
                {
                    AgentCurPathIndicies[agentIndex] = -1;
                    AgentData agent = AgentDataArray[agentIndex];
                    agent.Status = 0;
                    AgentDataArray[agentIndex] = agent;
                }
            }
        }
    }

}
