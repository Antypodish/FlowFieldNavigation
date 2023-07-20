using JetBrains.Annotations;
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
    public NativeList<Vector3> AgentPositions;
    public List<AgentPath> Paths;

    PathfindingManager _pathfindingManager;

    public AgentDataContainer(PathfindingManager manager)
    {
        _pathfindingManager = manager;
        Agents = new List<FlowFieldAgent>();
        Paths = new List<AgentPath>();
        AgentTransforms = new TransformAccessArray(0);
        AgentDataList = new NativeList<AgentData>(Allocator.Persistent);
        AgentPositions = new NativeList<Vector3>(Allocator.Persistent);
    }
    public void Subscribe(FlowFieldAgent agent)
    {
        agent.AgentDataIndex = Agents.Count;
        AgentData data = new AgentData()
        {
            Speed = agent.GetSpeed(),
            Destination = Vector2.zero,
            Direction = Vector2.zero,
        };
        Agents.Add(agent);
        Paths.Add(new AgentPath());
        AgentTransforms.Add(agent.transform);
        AgentDataList.Add(data);
        AgentPositions.Add(agent.transform.position);
    }
    public void UnSubscribe(FlowFieldAgent agent)
    {
        int agentIndex = agent.AgentDataIndex;
        agent.AgentDataIndex = -1;
        Agents.RemoveAtSwapBack(agentIndex);
        Paths.RemoveAtSwapBack(agentIndex);
        AgentTransforms.RemoveAtSwapBack(agentIndex);
        AgentDataList.RemoveAtSwapBack(agentIndex);
        AgentPositions.RemoveAtSwapBack(agentIndex);
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
    public void SetDirection(int agentIndex, Vector2 direction)
    {
        AgentData data = AgentDataList[agentIndex];
        data.Direction = direction;
        AgentDataList[agentIndex] = data;
    }
    public void SetDirection(NativeArray<AgentMovementData> agentMovementData)
    {
        AgentDirectionSetJob directionSetJob = new AgentDirectionSetJob()
        {
            AgentDataDataArray = AgentDataList,
            MovementDataArray = agentMovementData,
        };
        directionSetJob.Schedule().Complete();
    }
    public NativeArray<float2> GetPositionsOf(List<FlowFieldAgent> agents)
    {
        NativeArray<float2> positions = new NativeArray<float2>(agents.Count, Allocator.Persistent);
        for(int i = 0; i < agents.Count; i++)
        {
            Vector3 pos3d = AgentPositions[agents[i].AgentDataIndex];
            positions[i] = new float2(pos3d.x, pos3d.z);
        }
        return positions;
    }
}
public struct AgentData
{
    public float Speed;
    public float2 Destination;
    public float2 Direction;
}
public struct AgentPath
{
    public Path CurPath;
    public Path NewPath;
}