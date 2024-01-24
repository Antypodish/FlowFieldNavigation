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
    [SerializeField] float Radius;
    [SerializeField] float Speed;
    [SerializeField] float LandOffset;

    [HideInInspector] public Transform Transform;

    private void Start()
    {
        Transform = transform;
    }
    public int GetPathIndex()
    {
        return _pathfindingManager.Interface.GetPathIndex(AgentDataIndex);
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
