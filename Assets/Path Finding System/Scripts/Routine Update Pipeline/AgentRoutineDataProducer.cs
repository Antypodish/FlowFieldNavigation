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
using UnityEngine.Rendering;

public class AgentRoutineDataProducer
{
    AgentDataContainer _agentDataContainer;
    PathfindingManager _pathfindingManager;

    public NativeList<AgentMovementData> AgentMovementDataList;
    public NativeList<OutOfFieldStatus> AgentOutOfFieldStatusList;
    public NativeList<float2> AgentPositionChangeBuffer;
    public NativeList<RoutineResult> RoutineResults;
    public UnsafeList<UnsafeList<byte>> CostFieldList;
    public NativeArray<UnsafeList<HashTile>> HashGridArray;
    public NativeList<int> NormalToHashed; 
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
        int gridAmount = (int)math.ceil(FlowFieldUtilities.MaxAgentSize / FlowFieldUtilities.BaseSpatialGridSize);
        HashGridArray = new NativeArray<UnsafeList<HashTile>>(gridAmount, Allocator.Persistent);
        for(int i = 0; i < HashGridArray.Length; i++)
        {
            float fieldHorizontalSize = FlowFieldUtilities.FieldColAmount * FlowFieldUtilities.TileSize;
            float fieldVerticalSize = FlowFieldUtilities.FieldRowAmount * FlowFieldUtilities.TileSize;

            float gridTileSize = i * FlowFieldUtilities.BaseSpatialGridSize + FlowFieldUtilities.BaseSpatialGridSize;
            int gridColAmount = (int)math.ceil(fieldHorizontalSize / gridTileSize);
            int gridRowAmount = (int)math.ceil(fieldVerticalSize / gridTileSize);
            int gridSize = gridColAmount * gridRowAmount;
            UnsafeList<HashTile> grid = new UnsafeList<HashTile>(gridSize, Allocator.Persistent);
            grid.Length = gridSize;
            HashGridArray[i] = grid;
        }
        NormalToHashed = new NativeList<int>(Allocator.Persistent);
    }
    public AgentRoutineDataCalculationJob CalculateDirections()
    {
        NativeList<AgentData> agentDataList = _agentDataContainer.AgentDataList;
        NativeList<int> agentCurPaths = _agentDataContainer.AgentCurPathIndicies;
        List<Path> producedPaths = _pathfindingManager.PathProducer.ProducedPaths;
        TransformAccessArray agentTransforms = _agentDataContainer.AgentTransforms;

        
        //CLEAR
        AgentMovementDataList.Clear();
        AgentPositionChangeBuffer.Clear();
        RoutineResults.Clear();
        NormalToHashed.Clear();
        AgentMovementDataList.Length = agentDataList.Length;
        AgentOutOfFieldStatusList.Length = agentDataList.Length;
        RoutineResults.Length = agentDataList.Length;
        AgentPositionChangeBuffer.Length = agentDataList.Length;
        NormalToHashed.Length = agentDataList.Length;

        //SET POSITIONS OF AGENT DATA
        AgentDataSetPositionJob posSetJob = new AgentDataSetPositionJob()
        {
            AgentDataArray = agentDataList,
        };
        posSetJob.Schedule(agentTransforms).Complete();

        //SPATIAL HASHING
        AgentDataSpatialHasherJob spatialHasher = new AgentDataSpatialHasherJob()
        {
            BaseSpatialGridSize = FlowFieldUtilities.BaseSpatialGridSize,
            TileSize = FlowFieldUtilities.TileSize,
            FieldColAmount = FlowFieldUtilities.FieldColAmount,
            FieldRowAmount = FlowFieldUtilities.FieldRowAmount,
            MaxAgentSize = FlowFieldUtilities.MaxAgentSize,
            MinAgentSize = FlowFieldUtilities.MinAgentSize,
            AgentDataArray = agentDataList,
            AgentHashGridArray = HashGridArray,
            AgentMovementDataArray = AgentMovementDataList,
            NormalToHashed = NormalToHashed,
        };
        spatialHasher.Schedule().Complete();

        //FILL AGENT MOVEMENT DATA ARRAY
        for (int i = 0; i < agentDataList.Length; i++)
        {
            if (agentCurPaths[i] == -1) { continue; }
            Path curPath = producedPaths[agentCurPaths[i]];

            if (curPath == null) { continue; }
            int hashedIndex = NormalToHashed[i];
            AgentMovementData data = AgentMovementDataList[hashedIndex];
            data.FlowField = curPath.FlowField;
            data.Destination = curPath.Destination;
            data.SectorToPicked = curPath.SectorToPicked;
            data.Offset = curPath.Offset;
            data.PathId = curPath.Id;
            AgentMovementDataList[hashedIndex] = data;
        }
        
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