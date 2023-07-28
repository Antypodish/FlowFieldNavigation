using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Linq;
using TMPro;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEditor.Rendering.Universal;
using UnityEngine;
using UnityEngine.Jobs;

public class AgentDirectionCalculator
{
    AgentDataContainer _agentDataContainer;
    PathfindingManager _pathfindingManager;

    public NativeList<AgentMovementData> AgentMovementDataList;
    public NativeList<float2> Directions;

    public AgentDirectionCalculator(AgentDataContainer agentDataContainer, PathfindingManager pathfindingManager)
    {
        _agentDataContainer = agentDataContainer;
        _pathfindingManager = pathfindingManager;
        AgentMovementDataList = new NativeList<AgentMovementData>(_agentDataContainer.Agents.Count, Allocator.Persistent);
        Directions = new NativeList<float2>(Allocator.Persistent);
    }
    public AgentRoutineDataCalculationJob CalculateDirections(out TransformAccessArray transformsToSchedule)
    {
        NativeList<AgentData> agentDataList = _agentDataContainer.AgentDataList;
        List<AgentPath> pathList = _agentDataContainer.Paths;
        TransformAccessArray agentTransforms = _agentDataContainer.AgentTransforms;

        //CLEAR
        AgentMovementDataList.Clear();
        Directions.Clear();
        AgentMovementDataList.Length = agentDataList.Length;
        Directions.Length = agentDataList.Length;
        //FILL
        for (int i = 0; i < agentDataList.Length; i++)
        {
            Path curPath = pathList[i].CurPath;

            if (curPath == null)
            {
                AgentMovementData data = new AgentMovementData()
                {
                    Position = 0,
                    Radius = agentDataList[i].Radius,
                    Local1d = 0,
                    Flow = 0,
                    Sector1d = 0,
                    Speed = agentDataList[i].Speed,
                    OutOfFieldFlag = false,
                    PathId = -1,
                };
                AgentMovementDataList[i] = data;
            }
            else
            {
                AgentMovementData data = new AgentMovementData()
                {
                    Position = 0,
                    Radius = agentDataList[i].Radius,
                    Local1d = 0,
                    Flow = 0,
                    Sector1d = 0,
                    Speed = agentDataList[i].Speed,
                    Destination = agentDataList[i].Destination,
                    OutOfFieldFlag = false,
                    FlowField = curPath.FlowField,
                    SectorToPicked = curPath.SectorToPicked,
                    PathId = curPath.Id,
                };
                AgentMovementDataList[i] = data;
            }
        }
        
        //RETRUN JOB
        transformsToSchedule = agentTransforms;
        return new AgentRoutineDataCalculationJob()
        {
            TileSize = _pathfindingManager.TileSize,
            SectorColAmount = _pathfindingManager.SectorColAmount,
            SectorMatrixColAmount = _pathfindingManager.SectorMatrixColAmount,
            AgentMovementData = AgentMovementDataList,
        };
    }
    public void SendDirections()
    {
        _agentDataContainer.SetDirection(Directions);
    }
}
public struct AgentMovementData
{
    public float3 Position;
    public float2 Destination;
    public float2 Flow;
    public float Speed;
    public float Radius;
    public ushort Local1d;
    public ushort Sector1d;
    public bool OutOfFieldFlag;
    public UnsafeList<FlowData> FlowField;
    public UnsafeList<int> SectorToPicked;
    public int PathId;
}