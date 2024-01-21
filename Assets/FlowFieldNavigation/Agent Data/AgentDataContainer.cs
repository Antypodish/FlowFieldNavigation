using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;

public class AgentDataContainer
{
    public List<FlowFieldAgent> Agents;
    public TransformAccessArray AgentTransforms;
    public NativeList<AgentData> AgentDataList;
    public NativeList<bool> AgentDestinationReachedArray;
    public NativeList<int> AgentFlockIndicies;
    public NativeList<int> AgentRequestedPathIndicies;
    public NativeList<int> AgentNewPathIndicies;
    public NativeList<int> AgentCurPathIndicies;

    public AgentDataContainer()
    {
        Agents = new List<FlowFieldAgent>();
        AgentTransforms = new TransformAccessArray(0);
        AgentDataList = new NativeList<AgentData>(Allocator.Persistent);
        AgentNewPathIndicies = new NativeList<int>(0, Allocator.Persistent);
        AgentCurPathIndicies = new NativeList<int>(0, Allocator.Persistent);
        AgentRequestedPathIndicies = new NativeList<int>(0, Allocator.Persistent);
        AgentFlockIndicies = new NativeList<int>(Allocator.Persistent);
        AgentDestinationReachedArray = new NativeList<bool>(Allocator.Persistent);
    }
    public void Subscribe(FlowFieldAgent agent)
    {
        agent.AgentDataIndex = Agents.Count;
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
            int agentIndex = agents[i].AgentDataIndex;
            reqPathIndicies[agentIndex] = newPathIndex;
        }
    }
}