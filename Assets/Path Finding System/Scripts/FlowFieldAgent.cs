using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

using UnityEngine;

public class FlowFieldAgent : MonoBehaviour
{
    PathfindingManager _pathfindingManager;
    [SerializeField] float Radius;
    [SerializeField] float Speed;
    [SerializeField] float LandOffset;

    [HideInInspector] public int AgentDataIndex;
    [HideInInspector] public Transform Transform;

    private void Start()
    {
        _pathfindingManager = FindObjectOfType<PathfindingManager>();
        _pathfindingManager.Subscribe(this);
        Transform = transform;
    }
    public void SetPath(Path path)
    {
        _pathfindingManager.SetPath(AgentDataIndex, path);
    }
    public Path GetPath()
    {
        return _pathfindingManager.GetPath(AgentDataIndex);
    }
    public float GetSpeed() => Speed;
    public float GetRadius() => Radius;
    public float GetLandOffset() => LandOffset;
    public AgentStatus GetAgentStatus() => _pathfindingManager.AgentDataContainer.AgentDataList[AgentDataIndex].Status;
    public void Stop()
    {
        _pathfindingManager.AgentDataContainer.Stop(AgentDataIndex);
    }
    public void Mobilize()
    {
        _pathfindingManager.AgentDataContainer.Mobilize(AgentDataIndex);
    }
    public void SetSpeed(float newSpeed) { Speed = newSpeed; _pathfindingManager.AgentDataContainer.SetSpeed(AgentDataIndex, newSpeed); }
}
