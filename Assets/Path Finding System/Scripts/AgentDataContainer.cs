using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
    public List<AgentPath> Paths;

    PathfindingManager _pathfindingManager;

    public AgentDataContainer(PathfindingManager manager)
    {
        _pathfindingManager = manager;
        Agents = new List<FlowFieldAgent>();
        Paths = new List<AgentPath>();
        AgentTransforms = new TransformAccessArray(0);
        AgentDataList = new NativeList<AgentData>(Allocator.Persistent);
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
        };
        Agents.Add(agent);
        Paths.Add(new AgentPath());
        AgentTransforms.Add(agent.transform);
        AgentDataList.Add(data);
    }
    public void UnSubscribe(FlowFieldAgent agent)
    {
        int agentIndex = agent.AgentDataIndex;
        agent.AgentDataIndex = -1;
        Agents.RemoveAtSwapBack(agentIndex);
        Paths.RemoveAtSwapBack(agentIndex);
        AgentTransforms.RemoveAtSwapBack(agentIndex);
        AgentDataList.RemoveAtSwapBack(agentIndex);
        Agents[agentIndex].AgentDataIndex = agentIndex;
    }
    public void SetPath(int agentIndex, Path newPath)
    {
        AgentPath path = Paths[agentIndex];
        if (path.NewPath != null) { path.NewPath.Unsubscribe(); }
        path.NewPath = newPath;
        newPath.Subscribe();
        Paths[agentIndex] = path;
    }
    public Path GetPath(int agentIndex)
    {
        return Paths[agentIndex].CurPath;
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
    public void SetDirection(int agentIndex, Vector2 direction)
    {
        AgentData data = AgentDataList[agentIndex];
        data.Direction = direction * math.length(data.Direction);
        AgentDataList[agentIndex] = data;
    }
    public void SendRoutineResults(NativeArray<RoutineResult> routineResults, NativeArray<AgentMovementData> movementDataArray, NativeArray<float2> agentPositionChangeBuffer)
    {
        AgentPositionChangeSendJob posSendJob = new AgentPositionChangeSendJob()
        {
            AgentPositionChangeBuffer = agentPositionChangeBuffer,
        };
        posSendJob.Schedule(AgentTransforms).Complete();

        RoutineResultSendJob directionSetJob = new RoutineResultSendJob()
        {
            MovementDataArray = movementDataArray,
            AgentDataArray = AgentDataList,
            RoutineResultArray = routineResults,
        };
        directionSetJob.Schedule().Complete();
    }
    public NativeArray<float2> GetPositionsOf(List<FlowFieldAgent> agents)
    {
        NativeArray<float2> positions = new NativeArray<float2>(agents.Count, Allocator.Persistent);
        for(int i = 0; i < agents.Count; i++)
        {
            Vector3 pos3d = AgentDataList[agents[i].AgentDataIndex].Position;
            positions[i] = new float2(pos3d.x, pos3d.z);
        }
        return positions;
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
    public float2 Seperation;
    public float3 Position;
    public float Radius;
    public Waypoint waypoint;

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
public struct AgentPath
{
    public Path CurPath;
    public Path NewPath;
}
[Flags]
public enum AgentStatus : byte
{
    Moving = 1,
    HoldGround = 2,
}