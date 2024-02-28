using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Jobs;


namespace FlowFieldNavigation
{
    internal class AgentDataContainer
    {
        FlowFieldNavigationManager _navigationManager;

        internal TransformAccessArray AgentTransforms;
        internal NativeList<AgentData> AgentDataList;
        internal NativeList<int> AgentReferanceIndicies;
        internal NativeList<float> AgentRadii;
        internal NativeList<bool> AgentUseNavigationMovementFlags;
        internal NativeList<bool> AgentDestinationReachedArray;
        internal NativeList<int> AgentFlockIndicies;
        internal NativeList<int> AgentRequestedPathIndicies;
        internal NativeList<int> AgentNewPathIndicies;
        internal NativeList<int> AgentCurPathIndicies;
        public AgentDataContainer(FlowFieldNavigationManager navigationManager)
        {
            _navigationManager = navigationManager;
            AgentTransforms = new TransformAccessArray(0);
            AgentDataList = new NativeList<AgentData>(Allocator.Persistent);
            AgentNewPathIndicies = new NativeList<int>(0, Allocator.Persistent);
            AgentCurPathIndicies = new NativeList<int>(0, Allocator.Persistent);
            AgentRequestedPathIndicies = new NativeList<int>(0, Allocator.Persistent);
            AgentFlockIndicies = new NativeList<int>(Allocator.Persistent);
            AgentDestinationReachedArray = new NativeList<bool>(Allocator.Persistent);
            AgentUseNavigationMovementFlags = new NativeList<bool>(Allocator.Persistent);
            AgentRadii = new NativeList<float>(Allocator.Persistent);
            AgentUseNavigationMovementFlags = new NativeList<bool>(Allocator.Persistent);
            AgentReferanceIndicies = new NativeList<int>(Allocator.Persistent);
        }
        public void DisposeAll()
        {
            AgentTransforms.Dispose();
            AgentDataList.Dispose();
            AgentDestinationReachedArray.Dispose();
            AgentFlockIndicies.Dispose();
            AgentRequestedPathIndicies.Dispose();
            AgentNewPathIndicies.Dispose();
            AgentCurPathIndicies.Dispose();
            AgentRadii.Dispose();
            AgentUseNavigationMovementFlags.Dispose();
            AgentReferanceIndicies.Dispose();
        }
        public int Subscribe(AgentInput agentInput, Transform agentTransform)
        {
            int agentDataIndex = AgentDataList.Length;
            AgentData data = new AgentData()
            {
                Speed = agentInput.Speed,
                Status = 0,
                Destination = Vector2.zero,
                Direction = Vector2.zero,
                LandOffset = agentInput.LandOffset,
            };
            AgentRadii.Add(Mathf.Min(agentInput.Radius, FlowFieldUtilities.MaxAgentSize));
            AgentTransforms.Add(agentTransform);
            AgentDataList.Add(data);
            AgentNewPathIndicies.Add(-1);
            AgentCurPathIndicies.Add(-1);
            AgentRequestedPathIndicies.Add(-1);
            AgentFlockIndicies.Add(0);
            AgentDestinationReachedArray.Add(false);
            AgentUseNavigationMovementFlags.Add(true);
            return agentDataIndex;
        }
        public void SetRequestedPathIndiciesOf(List<FlowFieldAgent> agents, int newPathIndex)
        {
            NativeArray<int> reqPathIndicies = AgentRequestedPathIndicies.AsArray();
            for (int i = 0; i < agents.Count; i++)
            {
                FlowFieldAgent agent = agents[i];
                if (!agent.AgentReferance.IsValid()) { continue; }
                int agentDataIndex = _navigationManager.AgentReferanceManager.AgentDataReferanceIndexToAgentDataIndex(agent.AgentReferance.GetIndexNonchecked());
                reqPathIndicies[agentDataIndex] = newPathIndex;
            }
        }
    }
}