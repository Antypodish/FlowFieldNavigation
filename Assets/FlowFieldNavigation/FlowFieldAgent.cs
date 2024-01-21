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
    public void Stop()
    {
        _pathfindingManager.AgentDataContainer.Stop(AgentDataIndex);
    }
    public void SetHoldGround()
    {
        Stop();
        _pathfindingManager.AgentDataContainer.SetHoldGround(AgentDataIndex);
    }
}
