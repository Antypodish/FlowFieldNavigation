using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;

namespace FlowFieldNavigation
{
    internal class AgentRemovingSystem
    {
        FlowFieldNavigationManager _navigationManager;
        NativeList<int> _agentRemovalMarks;//(index == -1:nothing)(index == -2:removed)(index >= 0: index redirection which means it can be used as new index)
        public AgentRemovingSystem(FlowFieldNavigationManager navigationManager)
        {
            _navigationManager = navigationManager;
            _agentRemovalMarks = new NativeList<int>(Allocator.Persistent);
        }
        internal void RemoveAgents(NativeArray<int> agentIndiciesToRemove)
        {
            if (agentIndiciesToRemove.Length == 0) { return; }
            List<FlowFieldAgent> agents = _navigationManager.AgentDataContainer.Agents;
            TransformAccessArray agentTransforms = _navigationManager.AgentDataContainer.AgentTransforms;
            NativeList<AgentData> agentDataList = _navigationManager.AgentDataContainer.AgentDataList;
            NativeList<float> agentRadii = _navigationManager.AgentDataContainer.AgentRadii;
            NativeList<bool> agentDestinationReachedArray = _navigationManager.AgentDataContainer.AgentDestinationReachedArray;
            NativeList<int> agentFlockIndicies = _navigationManager.AgentDataContainer.AgentFlockIndicies;
            NativeList<int> agentRequestedPathIndicies = _navigationManager.AgentDataContainer.AgentRequestedPathIndicies;
            NativeList<int> agentNewPathIndicies = _navigationManager.AgentDataContainer.AgentNewPathIndicies;
            NativeList<int> agentCurPathIndicies = _navigationManager.AgentDataContainer.AgentCurPathIndicies;
            NativeList<Flock> flockList = _navigationManager.FlockDataContainer.FlockList;
            NativeList<int> pathSubscriberCounts = _navigationManager.PathDataContainer.PathSubscriberCounts;
            NativeArray<PathState> pathStates = _navigationManager.PathDataContainer.ExposedPathStateList.AsArray();
            NativeArray<PathDestinationData> pathDestinationArray = _navigationManager.PathDataContainer.PathDestinationDataList.AsArray();
            NativeArray<PathRequest> pathRequests = _navigationManager.RequestAccumulator.PathRequests.AsArray();
            NativeList<int> agentsLookingForPath = _navigationManager.PathfindingManager.AgentsLookingForPath;
            NativeList<PathRequestRecord> agentsLookingForPathRecords = _navigationManager.PathfindingManager.AgentsLookingForPathRecords;

            _agentRemovalMarks.Length = agents.Count;

            AgentRemovalMarkJob agentMark = new AgentRemovalMarkJob()
            {
                CurAgentCount = agentDataList.Length,
                AgentRemovalMarks = _agentRemovalMarks,
                RemovedAgentIndicies = agentIndiciesToRemove,
            };
            JobHandle agentMarkHandle = agentMark.Schedule();

            AgentDependencyUnsubJob agentDependencyUnsub = new AgentDependencyUnsubJob()
            {
                AgentCurPathIndicies = agentCurPathIndicies,
                AgentFlockIndicies = agentFlockIndicies,
                AgentRequestedPathIndicies = agentRequestedPathIndicies,
                FlockList = flockList.AsArray(),
                PathSubscriberCounts = pathSubscriberCounts.AsArray(),
                RemovedAgentIndicies = agentIndiciesToRemove,
            };
            JobHandle agentDependencyUnsubHandle = agentDependencyUnsub.Schedule(agentMarkHandle);

            NativeList<IndexShiftingPair> indexShiftingPairs = new NativeList<IndexShiftingPair>(Allocator.TempJob);
            NativeReference<int> lengthAfterRemoval = new NativeReference<int>(0, Allocator.TempJob);
            AgentRemovalIndexShiftingJob agentRemovalIndexShifting = new AgentRemovalIndexShiftingJob()
            {
                AgentCurPathIndicies = agentCurPathIndicies,
                AgentDataList = agentDataList,
                AgentRadii = agentRadii,
                AgentDestinationReachedArray = agentDestinationReachedArray,
                AgentFlockIndicies = agentFlockIndicies,
                AgentIndiciesToRemove = agentIndiciesToRemove,
                AgentNewPathIndicies = agentNewPathIndicies,
                AgentRequestedPathIndicies = agentRequestedPathIndicies,
                RemovedAgentMarks = _agentRemovalMarks,
                IndexShiftingPairs = indexShiftingPairs,
                LengthAfterRemoval = lengthAfterRemoval,
            };
            JobHandle agentRemovalIndexShiftingHandle = agentRemovalIndexShifting.Schedule(agentDependencyUnsubHandle);
            agentRemovalIndexShiftingHandle.Complete();

            for (int i = 0; i < indexShiftingPairs.Length; i++)
            {
                IndexShiftingPair pair = indexShiftingPairs[i];
                FlowFieldAgent agentBehaviour = agents[pair.Source];
                agentBehaviour.AgentDataIndex = pair.Destination;
                agents[pair.Destination] = agentBehaviour;

                agentTransforms[pair.Destination] = agentTransforms[pair.Source];
            }
            int removedAgentCount = agentIndiciesToRemove.Length;
            agents.RemoveRange(lengthAfterRemoval.Value, removedAgentCount);
            for (int i = 0; i < removedAgentCount; i++)
            {
                agentTransforms.RemoveAtSwapBack(lengthAfterRemoval.Value);
            }

            AgentRemovalPathRequestCleanupJob pathRequestCleanup = new AgentRemovalPathRequestCleanupJob()
            {
                AgentRemovalMarks = _agentRemovalMarks.AsArray(),
                AgentRequestedPathIndicies = agentRequestedPathIndicies,
                PathRequests = pathRequests,
            };
            JobHandle pathRequestCleanupHandle = pathRequestCleanup.Schedule();

            AgentRemovalLookingForPathListCleanupJob lookingForPathListCleanup = new AgentRemovalLookingForPathListCleanupJob()
            {
                AgentRemovalMarks = _agentRemovalMarks.AsArray(),
                AgentsLookingForPath = agentsLookingForPath,
                AgentsLookingForPathRecords = agentsLookingForPathRecords,
            };
            JobHandle lookingForPathListCleanupHandle = lookingForPathListCleanup.Schedule(pathRequestCleanupHandle);

            AgentRemovalPathCleanupJob pathCleanup = new AgentRemovalPathCleanupJob()
            {
                AgentDataArray = agentDataList.AsArray(),
                AgentCurPathIndicies = agentCurPathIndicies.AsArray(),
                AgentRemovalMarks = _agentRemovalMarks.AsArray(),
                PathStates = pathStates,
                PathSubscriberCounts = pathSubscriberCounts.AsArray(),
                PathDestinationDataArray = pathDestinationArray,
            };
            JobHandle pathCleanupHandle = pathCleanup.Schedule(lookingForPathListCleanupHandle);
            pathCleanupHandle.Complete();

            indexShiftingPairs.Dispose();
            lengthAfterRemoval.Dispose();
        }
    }

}
