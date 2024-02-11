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
}
