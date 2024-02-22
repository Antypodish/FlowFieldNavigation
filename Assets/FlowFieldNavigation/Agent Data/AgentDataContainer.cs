using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;


namespace FlowFieldNavigation
{
    internal class AgentDataContainer
    {
        FlowFieldNavigationManager _navigationManager;

        internal List<FlowFieldAgent> Agents;
        internal TransformAccessArray AgentTransforms;
        internal NativeList<AgentData> AgentDataList;
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
            Agents = new List<FlowFieldAgent>();
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
        }
        public void DisposeAll()
        {
            for (int i = 0; i < Agents.Count; i++)
            {
                Agents[i].AgentDataIndex = -1;
            }
            Agents.Clear();
            Agents.TrimExcess();
            Agents = null;
            AgentTransforms.Dispose();
            AgentDataList.Dispose();
            AgentDestinationReachedArray.Dispose();
            AgentFlockIndicies.Dispose();
            AgentRequestedPathIndicies.Dispose();
            AgentNewPathIndicies.Dispose();
            AgentCurPathIndicies.Dispose();
            AgentRadii.Dispose();
            AgentUseNavigationMovementFlags.Dispose();
        }
        public void Subscribe(FlowFieldAgent agent)
        {
            agent.AgentDataIndex = Agents.Count;
            agent._navigationManager = _navigationManager;
            AgentData data = new AgentData()
            {
                Speed = agent.Speed,
                Status = 0,
                Destination = Vector2.zero,
                Direction = Vector2.zero,
                LandOffset = agent.LandOffset,
            };
            Agents.Add(agent);
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
                if (agent.AgentDataIndex == -1) { continue; }
                reqPathIndicies[agent.AgentDataIndex] = newPathIndex;
            }
        }
    }
}