using JetBrains.Annotations;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;

public class AgentDataContainer
{
    public List<FlowFieldAgent> Agents;
    public List<Transform> AgentTransforms;
    public NativeList<AgentData> AgentDatas;
    public List<AgentPaths> AgentPaths;

    PathfindingManager _pathfindingManager;

    public AgentDataContainer(PathfindingManager manager)
    {
        _pathfindingManager = manager;
        Agents = new List<FlowFieldAgent>();
        AgentTransforms = new List<Transform>();
        AgentDatas = new NativeList<AgentData>(Allocator.Persistent);
        AgentPaths = new List<AgentPaths>();
    }
    public void OnStart()
    {

    }
    public void OnUpdate()
    {
        for(int i = 0; i < Agents.Count; i++)
        {
            AgentPaths paths = AgentPaths[i];
            AgentData data = AgentDatas[i];
            Transform transform = AgentTransforms[i];
            //REFRESH PATH
            if (paths.NewPath != null)
            {
                if (paths.NewPath.IsCalculated)
                {
                    if (paths.CurPath != null) { paths.CurPath.Unsubscribe(); }
                    paths.NewPath.Subscribe();
                    paths.CurPath = paths.NewPath;
                    data.Destination = paths.NewPath.Destination;
                    paths.NewPath = null;
                    AgentPaths[i] = paths;
                    AgentDatas[i] = data;
                }
            }
            //MOVE
            if (paths.CurPath != null)
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
        };
        AgentPaths paths = new AgentPaths()
        {
            CurPath = null,
            NewPath = null,
        };

        Agents.Add(agent);
        AgentTransforms.Add(agent.transform);
        AgentDatas.Add(data);
        AgentPaths.Add(paths);
    }
    public void UnSubscribe(FlowFieldAgent agent)
    {
        int agentIndex = agent.AgentDataIndex;
        Agents.RemoveAt(agentIndex);
        AgentTransforms.RemoveAt(agentIndex);
        AgentDatas.RemoveAt(agentIndex);
        AgentPaths.RemoveAt(agentIndex);
        for(int i = agentIndex; i < Agents.Count; i++)
        {
            Agents[i].AgentDataIndex--;
        }
    }
    public void SetPath(int agentIndex, Path newPath)
    {
        AgentPaths paths = AgentPaths[agentIndex];
        paths.NewPath = newPath;
        AgentPaths[agentIndex] = paths;
    }
    public void SetSpeed(int agentIndex, float newSpeed)
    {
        AgentData data = AgentDatas[agentIndex];
        data.Speed = newSpeed;
        AgentDatas[agentIndex] = data;
    }
}
public struct AgentData
{
    public float Speed;
    public Vector2 Destination;
    public Vector2 Direction;
}
public struct AgentPaths
{
    public Path CurPath;
    public Path NewPath;
}