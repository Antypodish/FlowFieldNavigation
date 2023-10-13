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
    public NativeList<float2> AgentPositionChangeBuffer;
    public NativeList<RoutineResult> RoutineResults;
    public UnsafeList<UnsafeList<byte>> CostFieldList;
    public AgentRoutineDataProducer(AgentDataContainer agentDataContainer, PathfindingManager pathfindingManager)
    {
        _agentDataContainer = agentDataContainer;
        _pathfindingManager = pathfindingManager;
        AgentMovementDataList = new NativeList<AgentMovementData>(_agentDataContainer.Agents.Count, Allocator.Persistent);
        AgentOutOfFieldStatusList = new NativeList<OutOfFieldStatus>(Allocator.Persistent);
        RoutineResults = new NativeList<RoutineResult>(Allocator.Persistent);
        AgentPositionChangeBuffer = new NativeList<float2>(Allocator.Persistent);
        CostField[] costFields = _pathfindingManager.FieldProducer.GetAllCostFields();
        CostFieldList = new UnsafeList<UnsafeList<byte>>(costFields.Length, Allocator.Persistent);
        CostFieldList.Length = costFields.Length;
        for(int i = 0; i < CostFieldList.Length; i++)
        {
            CostFieldList[i] = costFields[i].CostsG;
        }

    }
    public AgentRoutineDataCalculationJob CalculateDirections()
    {
        NativeList<AgentData> agentDataList = _agentDataContainer.AgentDataList;
        List<AgentPath> pathList = _agentDataContainer.Paths;
        TransformAccessArray agentTransforms = _agentDataContainer.AgentTransforms;

        
        //CLEAR
        AgentMovementDataList.Clear();
        AgentPositionChangeBuffer.Clear();
        RoutineResults.Clear();
        AgentMovementDataList.Length = agentDataList.Length;
        AgentOutOfFieldStatusList.Length = agentDataList.Length;
        RoutineResults.Length = agentDataList.Length;
        AgentPositionChangeBuffer.Length = agentDataList.Length;

        //FILL AGENT MOVEMENT DATA ARRAY
        for (int i = 0; i < agentDataList.Length; i++)
        {
            Path curPath = pathList[i].CurPath;

            if (curPath == null) { continue; }
            AgentMovementData data = new AgentMovementData()
            {
                FlowField = curPath.FlowField,
                SectorToPicked = curPath.SectorToPicked,
                Offset = curPath.Offset,
                PathId = curPath.Id,
            };
            AgentMovementDataList[i] = data;
        }
        AgentMovementDataArrayPreperationJob movDataPrepJob = new AgentMovementDataArrayPreperationJob()
        {
            AgentDataArray = agentDataList,
            AgentMovementDataArray = AgentMovementDataList,
        };
        movDataPrepJob.Schedule(agentTransforms).Complete();
        
        //RETRUN JOB
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