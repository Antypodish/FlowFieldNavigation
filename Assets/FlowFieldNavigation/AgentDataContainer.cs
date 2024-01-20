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
    public void UnSubscribe(FlowFieldAgent agent)
    {
        int agentIndex = agent.AgentDataIndex;
        agent.AgentDataIndex = -1;
        Agents.RemoveAtSwapBack(agentIndex);
        AgentTransforms.RemoveAtSwapBack(agentIndex);
        AgentDataList.RemoveAtSwapBack(agentIndex);
        Agents[agentIndex].AgentDataIndex = agentIndex;
    }
    public void SetSpeed(int agentIndex, float newSpeed)
    {
        AgentData data = AgentDataList[agentIndex];
        data.Speed = newSpeed;
        AgentDataList[agentIndex] = data;
    }
    public void Stop(int agentIndex)
    {
        AgentData data = AgentDataList[agentIndex]; 
        data.Status = ~(~data.Status | AgentStatus.Moving);
        AgentDataList[agentIndex] = data;
    }
    public void Mobilize(int agentIndex)
    {
        AgentData data = AgentDataList[agentIndex];
        data.Status |= AgentStatus.Moving;
        AgentDataList[agentIndex] = data;
    }
    public void SetHoldGround(int agentIndex)
    {
        AgentData data = AgentDataList[agentIndex];
        data.Status |= AgentStatus.HoldGround;
        data.Avoidance = 0;
        AgentDataList[agentIndex] = data;
    }
    public void ClearHoldGround(int agentIndex)
    {
        AgentData data = AgentDataList[agentIndex];
        data.Status = ~(~data.Status | AgentStatus.HoldGround);
        AgentDataList[agentIndex] = data;
    }
    public void SendRoutineResults(NativeArray<RoutineResult> routineResults, NativeArray<AgentMovementData> movementDataArray, NativeArray<float3> agentPositionChangeBuffer, NativeArray<int> normalToHashed)
    {
        AgentPositionChangeSendJob posSendJob = new AgentPositionChangeSendJob()
        {
            AgentPositionChangeBuffer = agentPositionChangeBuffer,
            NormalToHashed = normalToHashed,
        };
        posSendJob.Schedule(AgentTransforms).Complete();

        RoutineResultSendJob directionSetJob = new RoutineResultSendJob()
        {
            AgentDestinationReachedArray = AgentDestinationReachedArray,
            AgentCurPathIndicies = AgentCurPathIndicies,
            MovementDataArray = movementDataArray,
            AgentDataArray = AgentDataList,
            RoutineResultArray = routineResults,
            NormalToHashed = normalToHashed
        };
        directionSetJob.Schedule().Complete();
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
public struct AgentData
{
    public float Speed;
    public AgentStatus Status;
    public AvoidanceStatus Avoidance;
    public MovingAvoidanceStatus MovingAvoidance;
    public float2 Destination;
    public float2 Direction;
    public float2 DesiredDirection;
    public float3 Seperation;
    public float3 Position;
    public float LandOffset;
    public float Radius;

    public byte SplitInterval;
    public byte SplitInfo;

    public int StopDistanceIndex;

    public void SetStatusBit(AgentStatus status)
    {
        Status |= status;
    }
    public void ClearStatusBit(AgentStatus status)
    {
        Status = ~(~Status | status);
    }
}
[Flags]
public enum AgentStatus : byte
{
    Moving = 1,
    HoldGround = 2,
}