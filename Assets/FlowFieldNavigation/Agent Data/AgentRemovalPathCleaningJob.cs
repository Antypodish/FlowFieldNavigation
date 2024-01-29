using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;
using Unity.Mathematics;

[BurstCompile]
internal struct AgentRemovalPathCleaningJob : IJob
{
    [ReadOnly] internal NativeArray<int> RemovedAgentIndicies;
    [ReadOnly] internal NativeArray<bool> AgentRemovedFlags;
    [ReadOnly] internal NativeArray<PathDestinationData> PathDestinationDataArray;
    internal NativeArray<PathState> PathStates;
    internal NativeArray<int> AgentCurPathIndicies;
    internal NativeArray<int> AgentNewPathIndicies;
    internal NativeList<AgentAndPath> AgentsLookingForPathRecord;
    internal NativeList<int> AgentsLookingForPathIndicies;
    internal NativeArray<int> PathSubscriberCounts;
    public void Execute()
    {
        //Unsub removed agents from their paths
        for(int i = 0; i < RemovedAgentIndicies.Length; i++)
        {
            int agentIndex = RemovedAgentIndicies[i];
            //Unsub new path
            AgentNewPathIndicies[agentIndex] = -1;

            //Ubsub cur path
            int agentCurPathIndex = AgentCurPathIndicies[agentIndex];
            if(agentCurPathIndex != -1)
            {
                int pathSubscriberCount = PathSubscriberCounts[agentCurPathIndex];
                pathSubscriberCount--;
                PathSubscriberCounts[agentCurPathIndex] = pathSubscriberCount;
                AgentCurPathIndicies[agentIndex] = -1;
            }
        }

        //Remove removed agents from agentsLookingForPath lists
        for(int i = AgentsLookingForPathIndicies.Length - 1; i >= 0; i--)
        {
            int agentIndex = AgentsLookingForPathIndicies[i];
            if (AgentRemovedFlags[agentIndex])
            {
                AgentsLookingForPathIndicies.RemoveAtSwapBack(i);
                AgentsLookingForPathRecord.RemoveAtSwapBack(i);
            }
        }

        //Remove paths pointing towards those agents
        for(int pathIndex = 0; pathIndex< PathStates.Length; pathIndex++)
        {
            PathState pathState = PathStates[pathIndex];
            if(pathState == PathState.Removed) { continue; }
            PathDestinationData destinationData = PathDestinationDataArray[pathIndex];
            if(destinationData.DestinationType == DestinationType.StaticDestination) { continue; }
            int targetAgentIndex = destinationData.TargetAgentIndex;
            if (AgentRemovedFlags[targetAgentIndex])
            {
                PathSubscriberCounts[pathIndex] = 0;
            }
        }

        //Handle agents pointing towards removed path
        for(int agentIndex = 0; agentIndex < AgentCurPathIndicies.Length; agentIndex++)
        {
            if (AgentRemovedFlags[agentIndex]) { continue; }
            int curPathIndex = AgentCurPathIndicies[agentIndex];
            if(curPathIndex == -1) { continue; }
            int pointedPathSubscriberCount = PathSubscriberCounts[curPathIndex];
            if(pointedPathSubscriberCount == 0)
            {
                AgentCurPathIndicies[agentIndex] = -1;
            }
        }
    }
}
