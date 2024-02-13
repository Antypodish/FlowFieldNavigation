using System;
using System.Diagnostics.CodeAnalysis;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

using UnityEngine;

public class FlowFieldAgent : MonoBehaviour
{
    internal PathfindingManager _pathfindingManager;
    internal int AgentDataIndex = -1;   //-1 means not subscribed yet
    [SerializeField] internal float Radius;
    [SerializeField] internal float Speed;
    [SerializeField] internal float LandOffset;

    [HideInInspector] public Transform Transform;

    private void Start()
    {
        Transform = transform;
    }
    public int GetPathIndex()
    {
        return _pathfindingManager.Interface.GetPathIndex(AgentDataIndex);
    }
    public float GetSpeed()
    {
        if(_pathfindingManager == null) { return 0; }
        return _pathfindingManager.Interface.GetSpeed(this);
    }
    public AgentStatus GetStatus()
    {
        if (_pathfindingManager == null) { return 0; }
        return _pathfindingManager.Interface.GetStatus(this);
    }
    public void RequestSubscription()
    {
        if (_pathfindingManager == null) { return; }
        _pathfindingManager.Interface.RequestSubscription(this);
    }
    public void RequestUnsubscription()
    {
        if (_pathfindingManager == null) { return; }
        _pathfindingManager.Interface.RequestUnsubscription(this);
    }
    public void SetHoldGround()
    {
        if (_pathfindingManager == null) { return; }
        _pathfindingManager.Interface.SetHoldGround(this);
    }
    public void SetStopped()
    {
        if (_pathfindingManager == null) { return; }
        _pathfindingManager.Interface.SetStopped(this);
    }
    public void SetSpeed(float speed)
    {
        if (_pathfindingManager == null) { return; }
        _pathfindingManager.Interface.SetSpeed(this, speed);
    }
    public Vector3 GetCurrentDirection()
    {
        if(_pathfindingManager == null) { return Vector3.zero; }
        return _pathfindingManager.Interface.GetCurrentDirection(this);
    }
}
