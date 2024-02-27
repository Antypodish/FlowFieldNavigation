﻿using System.Collections.Generic;
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
        public void Subscribe(FlowFieldAgent agent)
        {
            int agentDataIndex = AgentDataList.Length;
            int agentReferanceIndex = _navigationManager.AgentReferanceManager.CreateAgentReferance();
            _navigationManager.AgentReferanceManager.AgentDataReferances[agentReferanceIndex] = new AgentDataReferance(agentDataIndex);
            agent.AgentReferance = new AgentReferance(agentReferanceIndex);
            agent._navigationManager = _navigationManager;
            AgentData data = new AgentData()
            {
                Speed = agent.Speed,
                Status = 0,
                Destination = Vector2.zero,
                Direction = Vector2.zero,
                LandOffset = agent.LandOffset,
            };
            AgentReferanceIndicies.Add(agentReferanceIndex);
            AgentRadii.Add(Mathf.Min(agent.Radius, FlowFieldUtilities.MaxAgentSize));
            AgentTransforms.Add(agent.transform);
            AgentDataList.Add(data);
            AgentNewPathIndicies.Add(-1);
            AgentCurPathIndicies.Add(-1);
            AgentRequestedPathIndicies.Add(-1);
            AgentFlockIndicies.Add(0);
            AgentDestinationReachedArray.Add(false);
            AgentUseNavigationMovementFlags.Add(true);
        }
        public void SetRequestedPathIndiciesOf(List<FlowFieldAgent> agents, int newPathIndex)
        {
            NativeArray<int> reqPathIndicies = AgentRequestedPathIndicies.AsArray();
            for (int i = 0; i < agents.Count; i++)
            {
                FlowFieldAgent agent = agents[i];
                if (!agent.AgentReferance.IsValid()) { continue; }
                int agentDataIndex = _navigationManager.AgentReferanceManager.AgentReferanceToAgentDataIndex(agent.AgentReferance);
                reqPathIndicies[agentDataIndex] = newPathIndex;
            }
        }
    }
}