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

public class AgentRoutineDataProducer
{
    AgentDataContainer _agentDataContainer;
    PathfindingManager _pathfindingManager;

    public NativeList<AgentMovementData> AgentMovementDataList;
    public NativeList<OutOfFieldStatus> AgentOutOfFieldStatusList;
    public NativeList<float2> ResultVelocities;
    public UnsafeList<UnsafeList<byte>> CostFieldList;
    public AgentRoutineDataProducer(AgentDataContainer agentDataContainer, PathfindingManager pathfindingManager)
    {
        _agentDataContainer = agentDataContainer;
        _pathfindingManager = pathfindingManager;
        AgentMovementDataList = new NativeList<AgentMovementData>(_agentDataContainer.Agents.Count, Allocator.Persistent);
        AgentOutOfFieldStatusList = new NativeList<OutOfFieldStatus>(Allocator.Persistent);
        ResultVelocities = new NativeList<float2>(Allocator.Persistent);
        CostField[] costFields = _pathfindingManager.FieldProducer.GetAllCostFields();
        CostFieldList = new UnsafeList<UnsafeList<byte>>(costFields.Length, Allocator.Persistent);
        CostFieldList.Length = costFields.Length;
        for(int i = 0; i < CostFieldList.Length; i++)
        {
            CostFieldList[i] = costFields[i].CostsG;
        }

    }
    public AgentRoutineDataCalculationJob CalculateDirections(out TransformAccessArray transformsToSchedule)
    {
        NativeList<AgentData> agentDataList = _agentDataContainer.AgentDataList;
        List<AgentPath> pathList = _agentDataContainer.Paths;
        TransformAccessArray agentTransforms = _agentDataContainer.AgentTransforms;

        //CLEAR
        AgentMovementDataList.Clear();
        ResultVelocities.Clear();
        AgentMovementDataList.Length = agentDataList.Length;
        AgentOutOfFieldStatusList.Length = agentDataList.Length;
        ResultVelocities.Length = agentDataList.Length;
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
                    Velocity = agentDataList[i].Velocity,
                    Speed = agentDataList[i].Speed,
                    Status = agentDataList[i].Status,
                    Avoidance = agentDataList[i].Avoidance,
                    RoutineStatus = 0,
                    PathId = -1,
                    TensionPowerIndex = -1,
                    
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
                    Velocity = agentDataList[i].Velocity,
                    Speed = agentDataList[i].Speed,
                    Destination = agentDataList[i].Destination,
                    Waypoint = agentDataList[i].waypoint,
                    Status = agentDataList[i].Status,
                    Avoidance = agentDataList[i].Avoidance,
                    RoutineStatus = 0,
                    FlowField = curPath.FlowField,
                    SectorToPicked = curPath.SectorToPicked,
                    Offset = curPath.Offset,
                    PathId = curPath.Id,
                    TensionPowerIndex = -1,
                };
                AgentMovementDataList[i] = data;
            }
        }
        
        //RETRUN JOB
        transformsToSchedule = agentTransforms;
        return new AgentRoutineDataCalculationJob()
        {
            AgentOutOfFieldStatusList = AgentOutOfFieldStatusList,
            FieldColAmount = _pathfindingManager.ColumnAmount,
            CostFields = CostFieldList,
            TileSize = _pathfindingManager.TileSize,
            SectorColAmount = _pathfindingManager.SectorColAmount,
            SectorMatrixColAmount = _pathfindingManager.SectorMatrixColAmount,
            AgentMovementData = AgentMovementDataList,
        };
    }
}