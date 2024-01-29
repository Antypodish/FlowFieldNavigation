using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;

internal class AgentRemovingSystem
{
    PathfindingManager _pathfindingManager;
    NativeList<int> _agentRemovalMarks;//(index == -1:nothing)(index == -2:removed)(index >= 0: index redirection which means it can be used as new index)
    public AgentRemovingSystem(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
        _agentRemovalMarks = new NativeList<int>(Allocator.Persistent);
    }
    internal void RemoveAgents(List<FlowFieldAgent> agentsToRemove)
    {
        List<FlowFieldAgent> agents = _pathfindingManager.AgentDataContainer.Agents;
        TransformAccessArray agentTransforms = _pathfindingManager.AgentDataContainer.AgentTransforms;
        NativeList<AgentData> agentDataList = _pathfindingManager.AgentDataContainer.AgentDataList;
        NativeList<bool> agentDestinationReachedArray = _pathfindingManager.AgentDataContainer.AgentDestinationReachedArray;
        NativeList<int> agentFlockIndicies = _pathfindingManager.AgentDataContainer.AgentFlockIndicies;
        NativeList<int> agentRequestedPathIndicies = _pathfindingManager.AgentDataContainer.AgentRequestedPathIndicies;
        NativeList<int> agentNewPathIndicies = _pathfindingManager.AgentDataContainer.AgentNewPathIndicies;
        NativeList<int> agentCurPathIndicies = _pathfindingManager.AgentDataContainer.AgentCurPathIndicies;
        NativeList<Flock> flockList = _pathfindingManager.FlockDataContainer.FlockList;
        NativeList<int> pathSubscriberCounts = _pathfindingManager.PathDataContainer.PathSubscriberCounts;
        NativeArray<PathState> pathStates = _pathfindingManager.PathDataContainer.ExposedPathStateList;
        NativeArray<PathDestinationData> pathDestinationArray = _pathfindingManager.PathDataContainer.PathDestinationDataList;
        NativeArray<PathRequest> pathRequests = _pathfindingManager.RequestAccumulator.PathRequests;
        NativeList<int> agentsLookingForPath = _pathfindingManager.PathConstructionPipeline.AgentsLookingForPath;
        NativeList<PathRequestRecord> agentsLookingForPathRecords = _pathfindingManager.PathConstructionPipeline.AgentsLookingForPathRecords;

        _agentRemovalMarks.Length = agents.Count;
        NativeList<int> removedAgentIndicies = new NativeList<int>(Allocator.TempJob);
        for(int i = 0; i < agentsToRemove.Count; i++)
        {
            FlowFieldAgent agentMonobehaviour = agentsToRemove[i];
            int agentIndex = agentMonobehaviour.AgentDataIndex;
            if(agentIndex == -1) { continue; }
            agentMonobehaviour.AgentDataIndex = -1;
            removedAgentIndicies.Add(agentIndex);
        }

        AgentRemovalMarkJob agentMark = new AgentRemovalMarkJob()
        {
            CurAgentCount = agentDataList.Length,
            AgentRemovalMarks = _agentRemovalMarks,
            RemovedAgentIndicies = removedAgentIndicies,
        };
        JobHandle agentMarkHandle = agentMark.Schedule();

        AgentDependencyUnsubJob agentDependencyUnsub = new AgentDependencyUnsubJob()
        {
            AgentCurPathIndicies = agentCurPathIndicies,
            AgentFlockIndicies = agentFlockIndicies,
            AgentRequestedPathIndicies = agentRequestedPathIndicies,
            FlockList = flockList,
            PathSubscriberCounts = pathSubscriberCounts,
            RemovedAgentIndicies = removedAgentIndicies,
        };
        JobHandle agentDependencyUnsubHandle = agentDependencyUnsub.Schedule(agentMarkHandle);

        NativeList<IndexShiftingPair> indexShiftingPairs = new NativeList<IndexShiftingPair>(Allocator.TempJob);
        NativeReference<int> lengthAfterRemoval = new NativeReference<int>(0, Allocator.TempJob);
        AgentRemovalIndexShiftingJob agentRemovalIndexShifting = new AgentRemovalIndexShiftingJob()
        {
            AgentCurPathIndicies = agentCurPathIndicies,
            AgentDataList = agentDataList,
            AgentDestinationReachedArray = agentDestinationReachedArray,
            AgentFlockIndicies = agentFlockIndicies,
            AgentIndiciesToRemove = removedAgentIndicies,
            AgentNewPathIndicies = agentNewPathIndicies,
            AgentRequestedPathIndicies = agentRequestedPathIndicies,
            RemovedAgentMarks = _agentRemovalMarks,
            IndexShiftingPairs = indexShiftingPairs,
            LengthAfterRemoval = lengthAfterRemoval,
        };
        JobHandle agentRemovalIndexShiftingHandle = agentRemovalIndexShifting.Schedule(agentDependencyUnsubHandle);
        agentRemovalIndexShiftingHandle.Complete();

        for(int i = 0; i < indexShiftingPairs.Length; i++)
        {
            IndexShiftingPair pair = indexShiftingPairs[i];
            FlowFieldAgent agentBehaviour = agents[pair.Source];
            agentBehaviour.AgentDataIndex = pair.Destination;
            agents[pair.Destination] = agentBehaviour;

            agentTransforms[pair.Destination] = agentTransforms[pair.Source];
        }
        int removedAgentCount = removedAgentIndicies.Length;
        agents.RemoveRange(lengthAfterRemoval.Value, removedAgentCount);
        for(int i = 0; i < removedAgentCount; i++)
        {
            agentTransforms.RemoveAtSwapBack(lengthAfterRemoval.Value);
        }

        AgentRemovalPathRequestCleanupJob pathRequestCleanup = new AgentRemovalPathRequestCleanupJob()
        {
            AgentRemovalMarks = _agentRemovalMarks,
            AgentRequestedPathIndicies = agentRequestedPathIndicies,
            PathRequests = pathRequests,
        };
        JobHandle pathRequestCleanupHandle = pathRequestCleanup.Schedule();

        AgentRemovalLookingForPathListCleanupJob lookingForPathListCleanup = new AgentRemovalLookingForPathListCleanupJob()
        {
            AgentRemovalMarks = _agentRemovalMarks,
            AgentsLookingForPath = agentsLookingForPath,
            AgentsLookingForPathRecords = agentsLookingForPathRecords,
        };
        JobHandle lookingForPathListCleanupHandle = lookingForPathListCleanup.Schedule(pathRequestCleanupHandle);

        AgentRemovalPathCleanupJob pathCleanup = new AgentRemovalPathCleanupJob()
        {
            AgentCurPathIndicies = agentCurPathIndicies,
            AgentRemovalMarks = _agentRemovalMarks,
            PathStates = pathStates,
            PathSubscriberCounts = pathSubscriberCounts,
            PathDestinationDataArray = pathDestinationArray,
        };
        JobHandle pathCleanupHandle = pathCleanup.Schedule(lookingForPathListCleanupHandle);
        pathCleanupHandle.Complete();

        removedAgentIndicies.Dispose();
        indexShiftingPairs.Dispose();
        lengthAfterRemoval.Dispose();
    }
}