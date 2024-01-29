using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;

internal class AgentDataContainer
{
    PathfindingManager _pathfindingManager;

    internal List<FlowFieldAgent> Agents;
    internal TransformAccessArray AgentTransforms;
    internal NativeList<AgentData> AgentDataList;
    internal NativeList<bool> AgentDestinationReachedArray;
    internal NativeList<int> AgentFlockIndicies;
    internal NativeList<int> AgentRequestedPathIndicies;
    internal NativeList<int> AgentNewPathIndicies;
    internal NativeList<int> AgentCurPathIndicies;
    internal NativeList<bool> AgentRemovedFlags;

    internal Stack<int> RemovedAgentIndicies;
    public AgentDataContainer(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
        Agents = new List<FlowFieldAgent>();
        AgentTransforms = new TransformAccessArray(0);
        AgentDataList = new NativeList<AgentData>(Allocator.Persistent);
        AgentNewPathIndicies = new NativeList<int>(0, Allocator.Persistent);
        AgentCurPathIndicies = new NativeList<int>(0, Allocator.Persistent);
        AgentRequestedPathIndicies = new NativeList<int>(0, Allocator.Persistent);
        AgentFlockIndicies = new NativeList<int>(Allocator.Persistent);
        AgentDestinationReachedArray = new NativeList<bool>(Allocator.Persistent);
        AgentRemovedFlags = new NativeList<bool>(Allocator.Persistent);
        RemovedAgentIndicies = new Stack<int>();
    }
    public void DisposeAll()
    {
        for(int i = 0; i < Agents.Count; i++)
        {
            Agents[i].AgentDataIndex = -1;
        }
        Agents.Clear();
        Agents.TrimExcess();
        Agents = null;
        RemovedAgentIndicies = null;
        AgentTransforms.Dispose();
        AgentDataList.Dispose();
        AgentDestinationReachedArray.Dispose();
        AgentFlockIndicies.Dispose();
        AgentRequestedPathIndicies.Dispose();
        AgentNewPathIndicies.Dispose();
        AgentCurPathIndicies.Dispose();
        AgentRemovedFlags.Dispose();
    }
    public void Subscribe(FlowFieldAgent agent)
    {
        if(RemovedAgentIndicies.Count > 0)
        {
            int agentIndex = RemovedAgentIndicies.Pop();
            agent.AgentDataIndex = agentIndex;
            agent._pathfindingManager = _pathfindingManager;
            AgentData data = new AgentData()
            {
                Speed = agent.GetSpeed(),
                Status = 0,
                Destination = Vector2.zero,
                Direction = Vector2.zero,
                Radius = agent.GetRadius(),
                Position = agent.transform.position,
                LandOffset = agent.GetLandOffset(),
            };
            Agents[agentIndex] = agent;
            AgentTransforms[agentIndex] = agent.transform;
            AgentDataList[agentIndex] = data;
            AgentNewPathIndicies[agentIndex] = -1;
            AgentCurPathIndicies[agentIndex] = -1;
            AgentRequestedPathIndicies[agentIndex] = -1;
            AgentFlockIndicies[agentIndex] = 0;
            AgentDestinationReachedArray[agentIndex] = false;
            AgentRemovedFlags[agentIndex] = false;
        }
        else
        {
            agent.AgentDataIndex = Agents.Count;
            agent._pathfindingManager = _pathfindingManager;
            AgentData data = new AgentData()
            {
                Speed = agent.GetSpeed(),
                Status = 0,
                Destination = Vector2.zero,
                Direction = Vector2.zero,
                Radius = agent.GetRadius(),
                Position = agent.transform.position,
                LandOffset = agent.GetLandOffset(),
            };
            Agents.Add(agent);
            AgentTransforms.Add(agent.transform);
            AgentDataList.Add(data);
            AgentNewPathIndicies.Add(-1);
            AgentCurPathIndicies.Add(-1);
            AgentRequestedPathIndicies.Add(-1);
            AgentFlockIndicies.Add(0);
            AgentDestinationReachedArray.Add(false);
            AgentRemovedFlags.Add(false);
        }
    }
    public void Unsubscribe(FlowFieldAgent agent)
    {
        if(agent.AgentDataIndex == -1) { return; }
        int agentIndex = agent.AgentDataIndex;
        agent.AgentDataIndex = -1;
        agent._pathfindingManager = null;
        AgentRemovedFlags[agentIndex] = true;
        RemovedAgentIndicies.Push(agentIndex);
        //I need to:
        //Clean paths
        //Clean flocks
        //Handle movement system
    }
    public void Stop(int agentIndex)
    {
        AgentData data = AgentDataList[agentIndex]; 
        data.Status = ~(~data.Status | AgentStatus.Moving);
        AgentDataList[agentIndex] = data;
    }
    public void SetHoldGround(int agentIndex)
    {
        AgentData data = AgentDataList[agentIndex];
        data.Status |= AgentStatus.HoldGround;
        data.Avoidance = 0;
        AgentDataList[agentIndex] = data;
    }
    public void SetRequestedPathIndiciesOf(List<FlowFieldAgent> agents, int newPathIndex)
    {
        NativeArray<int> reqPathIndicies = AgentRequestedPathIndicies;
        for(int i = 0; i < agents.Count; i++)
        {
            FlowFieldAgent agent = agents[i];
            if(agent.AgentDataIndex == -1) { continue; }
            reqPathIndicies[agent.AgentDataIndex] = newPathIndex;
        }
    }
}