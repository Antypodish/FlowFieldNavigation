using JetBrains.Annotations;
using System.Collections.Generic;
using System.Diagnostics;
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
    public List<AgentData> AgentDataList;

    PathfindingManager _pathfindingManager;

    public AgentDataContainer(PathfindingManager manager)
    {
        _pathfindingManager = manager;
        Agents = new List<FlowFieldAgent>();
        AgentTransforms = new TransformAccessArray(0);
        AgentDataList = new List<AgentData>();
    }
    public void OnStart()
    {

    }
    public void OnUpdate()
    {
        for(int i = 0; i < Agents.Count; i++)
        {
            AgentData data = AgentDataList[i];
            Transform transform = AgentTransforms[i];
            //REFRESH PATH
            if (data.NewPath != null)
            {
                if (data.NewPath.IsCalculated)
                {
                    if (data.CurPath != null) { data.CurPath.Unsubscribe(); }
                    data.NewPath.Subscribe();
                    data.CurPath = data.NewPath;
                    data.Destination = data.NewPath.Destination;
                    data.NewPath = null;
                    AgentDataList[i] = data;
                }
            }
            //MOVE
            if (data.CurPath != null)
            {
                if (data.Direction == Vector2.zero)
                {
                    Vector3 destination = new Vector3(data.Destination.x, transform.position.y, data.Destination.y);
                    transform.position = Vector3.MoveTowards(transform.position, destination, data.Speed * Time.deltaTime);
                }
                else
                {
                    Vector3 direction = new Vector3(data.Direction.x, 0f, data.Direction.y);
                    transform.position += direction * data.Speed * Time.deltaTime;
                }
            }
        }

    }
    public void OnTimedUpdate(float deltaTime)
    {

    }
    public void Subscribe(FlowFieldAgent agent)
    {
        agent.AgentDataIndex = Agents.Count;
        AgentData data = new AgentData()
        {
            Speed = agent.GetSpeed(),
            Destination = Vector2.zero,
            Direction = Vector2.zero,
            CurPath = null,
            NewPath = null,
        };
        Agents.Add(agent);
        AgentTransforms.Add(agent.transform);
        AgentDataList.Add(data);
    }
    public void UnSubscribe(FlowFieldAgent agent)
    {
        int agentIndex = agent.AgentDataIndex;
        Agents.RemoveAtSwapBack(agentIndex);
        AgentTransforms.RemoveAtSwapBack(agentIndex);
        AgentDataList.RemoveAtSwapBack(agentIndex);
        Agents[agentIndex].AgentDataIndex = agentIndex;
    }
    public void SetPath(int agentIndex, Path newPath)
    {
        AgentData data = AgentDataList[agentIndex];
        data.NewPath = newPath;
        AgentDataList[agentIndex] = data;
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
}
public struct AgentData
{
    public float Speed;
    public Vector2 Destination;
    public Vector2 Direction;
    public Path CurPath;
    public Path NewPath;
}