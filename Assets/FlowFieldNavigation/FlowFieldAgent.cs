using System;
using System.Diagnostics.CodeAnalysis;
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

    [HideInInspector] public int AgentDataIndex;   //-1 means not subscribed yet
    [HideInInspector] public Transform Transform;

    private void Start()
    {
        _pathfindingManager = FindObjectOfType<PathfindingManager>();
        _pathfindingManager.RequestSubscription(this);
        Transform = transform;
        AgentDataIndex = -1;
    }
    public int GetPathIndex()
    {
        return _pathfindingManager.GetPathIndex(AgentDataIndex);
    }
    public float GetSpeed() => Speed;
    public float GetRadius() => Radius;
    public float GetLandOffset() => LandOffset;
    public Vector2 GetDirection() => _pathfindingManager.AgentDataContainer.AgentDataList[AgentDataIndex].Direction;
    public AgentStatus GetAgentStatus() => _pathfindingManager.AgentDataContainer.AgentDataList[AgentDataIndex].Status;
    public void Stop()
    {
        _pathfindingManager.AgentDataContainer.Stop(AgentDataIndex);
    }
    public void Mobilize()
    {
        ClearHoldGround();
        _pathfindingManager.AgentDataContainer.Mobilize(AgentDataIndex);
    }
    public void SetHoldGround()
    {
        Stop();
        _pathfindingManager.AgentDataContainer.SetHoldGround(AgentDataIndex);
    }
    public void ClearHoldGround()
    {
        _pathfindingManager.AgentDataContainer.ClearHoldGround(AgentDataIndex);
    }
    public void SetSpeed(float newSpeed) { Speed = newSpeed; _pathfindingManager.AgentDataContainer.SetSpeed(AgentDataIndex, newSpeed); }
}
