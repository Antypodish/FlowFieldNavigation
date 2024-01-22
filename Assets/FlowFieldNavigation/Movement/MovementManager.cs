﻿using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Jobs;
using System.Diagnostics;
using System.Collections.Generic;
using UnityEngine;
internal class MovementManager
{
    AgentDataContainer _agentDataContainer;
    PathfindingManager _pathfindingManager;

    internal NativeList<AgentMovementData> AgentMovementDataList;
    internal NativeList<float3> AgentPositionChangeBuffer;
    internal NativeList<RoutineResult> RoutineResults;
    internal NativeList<int> NormalToHashed;
    internal NativeList<int> HashedToNormal;
    internal NativeArray<UnsafeList<HashTile>> HashGridArray;
    internal NativeList<float> PathReachDistances;

    JobHandle _routineHandle;
    internal MovementManager(AgentDataContainer agentDataContainer, PathfindingManager pathfindingManager)
    {
        _agentDataContainer = agentDataContainer;
        _pathfindingManager = pathfindingManager;
        AgentMovementDataList = new NativeList<AgentMovementData>(_agentDataContainer.Agents.Count, Allocator.Persistent);
        RoutineResults = new NativeList<RoutineResult>(Allocator.Persistent);
        AgentPositionChangeBuffer = new NativeList<float3>(Allocator.Persistent);
        _routineHandle = new JobHandle();

        //SPATIAL HASH GRID INSTANTIATION
        int gridAmount = (int)math.ceil(FlowFieldUtilities.MaxAgentSize / FlowFieldUtilities.BaseAgentSpatialGridSize);
        HashGridArray = new NativeArray<UnsafeList<HashTile>>(gridAmount, Allocator.Persistent);
        for(int i = 0; i < HashGridArray.Length; i++)
        {
            float fieldHorizontalSize = FlowFieldUtilities.FieldColAmount * FlowFieldUtilities.TileSize;
            float fieldVerticalSize = FlowFieldUtilities.FieldRowAmount * FlowFieldUtilities.TileSize;

            float gridTileSize = i * FlowFieldUtilities.BaseAgentSpatialGridSize + FlowFieldUtilities.BaseAgentSpatialGridSize;
            int gridColAmount = (int)math.ceil(fieldHorizontalSize / gridTileSize);
            int gridRowAmount = (int)math.ceil(fieldVerticalSize / gridTileSize);
            int gridSize = gridColAmount * gridRowAmount;
            UnsafeList<HashTile> grid = new UnsafeList<HashTile>(gridSize, Allocator.Persistent);
            grid.Length = gridSize;
            HashGridArray[i] = grid;
        }
        NormalToHashed = new NativeList<int>(Allocator.Persistent);
        HashedToNormal = new NativeList<int>(Allocator.Persistent);
        PathReachDistances = new NativeList<float>(Allocator.Persistent);
    }
    public void DisposeAll()
    {
        AgentMovementDataList.Dispose();
        AgentPositionChangeBuffer.Dispose();
        RoutineResults.Dispose();
        NormalToHashed.Dispose();
        HashedToNormal.Dispose();
        for(int i = 0; i < HashGridArray.Length; i++)
        {
            UnsafeList<HashTile> hashGrid = HashGridArray[i];
            hashGrid.Dispose();
        }
        HashGridArray.Dispose();
        PathReachDistances.Dispose();
    }
    internal void ScheduleRoutine(NativeArray<UnsafeListReadOnly<byte>> costFieldCosts, JobHandle dependency)
    {
        NativeArray<AgentData> agentDataArray = _agentDataContainer.AgentDataList;
        NativeArray<int> agentCurPathIndexArray = _agentDataContainer.AgentCurPathIndicies;
        NativeArray<PathLocationData> exposedPathLocationDataArray = _pathfindingManager.PathDataContainer.ExposedPathLocationData;
        NativeArray<PathFlowData> exposedPathFlowDataArray = _pathfindingManager.PathDataContainer.ExposedPathFlowData;
        NativeArray<float2> exposedPathDestinationArray = _pathfindingManager.PathDataContainer.ExposedPathDestinations;
        NativeArray<int> agentFlockIndexArray = _pathfindingManager.AgentDataContainer.AgentFlockIndicies;
        NativeArray<float> exposedPathReachDistanceCheckRanges = _pathfindingManager.PathDataContainer.ExposedPathReachDistanceCheckRanges;
        NativeArray<int> exposedPathFlockIndicies = _pathfindingManager.PathDataContainer.ExposedPathFlockIndicies;
        NativeArray<PathState> exposedPathStateList = _pathfindingManager.PathDataContainer.ExposedPathStateList;
        NativeArray<bool> exposedPathAgentStopFlagList = _pathfindingManager.PathDataContainer.ExposedPathAgentStopFlagList;

        //CLEAR
        AgentMovementDataList.Clear();
        AgentPositionChangeBuffer.Clear();
        RoutineResults.Clear();
        NormalToHashed.Clear();
        AgentMovementDataList.Length = agentDataArray.Length;
        RoutineResults.Length = agentDataArray.Length;
        AgentPositionChangeBuffer.Length = agentDataArray.Length;
        NormalToHashed.Length = agentDataArray.Length;
        HashedToNormal.Length = agentDataArray.Length;

        //SPATIAL HASHING
        AgentDataSpatialHasherJob spatialHasher = new AgentDataSpatialHasherJob()
        {
            BaseSpatialGridSize = FlowFieldUtilities.BaseAgentSpatialGridSize,
            TileSize = FlowFieldUtilities.TileSize,
            FieldColAmount = FlowFieldUtilities.FieldColAmount,
            FieldRowAmount = FlowFieldUtilities.FieldRowAmount,
            MaxAgentSize = FlowFieldUtilities.MaxAgentSize,
            MinAgentSize = FlowFieldUtilities.MinAgentSize,
            AgentDataArray = agentDataArray,
            AgentFlockIndexArray = agentFlockIndexArray,
            AgentHashGridArray = HashGridArray,
            AgentMovementDataArray = AgentMovementDataList,
            NormalToHashed = NormalToHashed,
            HashedToNormal = HashedToNormal,
        };
        JobHandle spatialHasherHandle = spatialHasher.Schedule(dependency);

        //FILL AGENT MOVEMENT DATA ARRAY
        float sectorSize = FlowFieldUtilities.SectorColAmount * FlowFieldUtilities.TileSize;
        int sectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount;
        AgentRoutineDataCalculationJob routineDataCalcJob = new AgentRoutineDataCalculationJob()
        {
            SectorMatrixColAmount = sectorMatrixColAmount,
            SectorSize = sectorSize,
            AgentCurPathIndicies = agentCurPathIndexArray,
            AgentDataArray = agentDataArray,
            ExposedPathDestinationArray = exposedPathDestinationArray,
            ExposedPathFlowDataArray = exposedPathFlowDataArray,
            ExposedPathLocationDataArray = exposedPathLocationDataArray,
            HashedToNormal = HashedToNormal,
            
            FieldColAmount = FlowFieldUtilities.FieldColAmount,
            TileSize = FlowFieldUtilities.TileSize,
            SectorColAmount = FlowFieldUtilities.SectorColAmount,
            AgentMovementData = AgentMovementDataList,
        };
        JobHandle movDataHandle = routineDataCalcJob.Schedule(routineDataCalcJob.AgentMovementData.Length, 64, spatialHasherHandle);

        //Height Calculation
        AgentHeightCalculationJob heightCalculation = new AgentHeightCalculationJob()
        {
            TriangleSpatialHashGrid = _pathfindingManager.FieldDataContainer.HeightMeshGenerator.GetTriangleSpatialHashGrid(),
            AgentMovementDataArray = AgentMovementDataList,
            AgentPositionChangeArray = AgentPositionChangeBuffer,
            Verticies = _pathfindingManager.FieldDataContainer.HeightMeshGenerator.Verticies,
        };
        JobHandle heightCalculationHandle = heightCalculation.Schedule(agentDataArray.Length, 32, movDataHandle);

        //SCHEDULE LOCAL AVODANCE JOB
        LocalAvoidanceJob avoidanceJob = new LocalAvoidanceJob()
        {
            FieldMaxXExcluding = FlowFieldUtilities.FieldMaxXExcluding,
            FieldMaxYExcluding = FlowFieldUtilities.FieldMaxYExcluding,
            FieldMinXIncluding = FlowFieldUtilities.FieldMinXIncluding,
            FieldMinYIncluding = FlowFieldUtilities.FieldMinYIncluding,
            SectorColAmount = FlowFieldUtilities.SectorColAmount,
            SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
            SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
            TileSize = FlowFieldUtilities.TileSize,
            SeperationMultiplier = BoidController.Instance.SeperationMultiplier,
            SeperationRangeAddition = BoidController.Instance.SeperationRangeAddition,
            SeekMultiplier = BoidController.Instance.SeekMultiplier,
            AlignmentMultiplier = BoidController.Instance.AlignmentMultiplier,
            AlignmentRangeAddition = BoidController.Instance.AlignmentRangeAddition,
            MovingAvoidanceRangeAddition = BoidController.Instance.MovingAvoidanceRangeAddition,
            BaseSpatialGridSize = FlowFieldUtilities.BaseAgentSpatialGridSize,
            FieldHorizontalSize = FlowFieldUtilities.TileSize * FlowFieldUtilities.FieldColAmount,
            FieldVerticalSize = FlowFieldUtilities.TileSize * FlowFieldUtilities.FieldRowAmount,
            RoutineResultArray = RoutineResults,
            AgentSpatialHashGrid = new AgentSpatialHashGrid()
            {
                BaseSpatialGridSize = FlowFieldUtilities.BaseAgentSpatialGridSize,
                FieldHorizontalSize = FlowFieldUtilities.TileSize * FlowFieldUtilities.FieldColAmount,
                FieldVerticalSize = FlowFieldUtilities.TileSize * FlowFieldUtilities.FieldRowAmount,
                AgentHashGridArray = HashGridArray,
                RawAgentMovementDataArray = AgentMovementDataList,
            },
            CostFieldEachOffset = costFieldCosts,
        };
        JobHandle avoidanceHandle = avoidanceJob.Schedule(agentDataArray.Length, 64, heightCalculationHandle);


        //SCHEDULE AGENT COLLISION JOB
        CollisionResolutionJob colResJob = new CollisionResolutionJob()
        {
            AgentSpatialHashGrid = new AgentSpatialHashGrid()
            {
                BaseSpatialGridSize = FlowFieldUtilities.BaseAgentSpatialGridSize,
                FieldHorizontalSize = FlowFieldUtilities.TileSize * FlowFieldUtilities.FieldColAmount,
                FieldVerticalSize = FlowFieldUtilities.TileSize * FlowFieldUtilities.FieldRowAmount,
                AgentHashGridArray = HashGridArray,
                RawAgentMovementDataArray = AgentMovementDataList,
            },
            RoutineResultArray = RoutineResults,
            AgentPositionChangeBuffer = AgentPositionChangeBuffer,
        };
        JobHandle colResHandle = colResJob.Schedule(agentDataArray.Length, 4, avoidanceHandle);

        //SCHEDULE TENSON RES JOB
        TensionResolver tensionResJob = new TensionResolver()
        {
            AgentSpatialHashGrid = new AgentSpatialHashGrid()
            {
                BaseSpatialGridSize = FlowFieldUtilities.BaseAgentSpatialGridSize,
                FieldHorizontalSize = FlowFieldUtilities.TileSize * FlowFieldUtilities.FieldColAmount,
                FieldVerticalSize = FlowFieldUtilities.TileSize * FlowFieldUtilities.FieldRowAmount,
                AgentHashGridArray = HashGridArray,
                RawAgentMovementDataArray = AgentMovementDataList,
            },
            RoutineResultArray = RoutineResults,
            SeperationRangeAddition = BoidController.Instance.SeperationRangeAddition,
        };
        JobHandle tensionHandle = tensionResJob.Schedule(colResHandle);

        //SCHEDULE WALL COLLISION JOB
        AgentWallCollisionJob wallCollision = new AgentWallCollisionJob()
        {
            SectorColAmount = FlowFieldUtilities.SectorColAmount,
            SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
            SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
            TileSize = FlowFieldUtilities.TileSize,
            FieldColAmount = FlowFieldUtilities.FieldColAmount,
            FieldRowAmount = FlowFieldUtilities.FieldRowAmount,
            SectorRowAmount = FlowFieldUtilities.SectorRowAmount,
            HalfTileSize = FlowFieldUtilities.TileSize / 2,
            AgentMovementData = AgentMovementDataList,
            AgentPositionChangeBuffer = AgentPositionChangeBuffer,
            CostFieldEachOffset = costFieldCosts,
        };
        JobHandle wallCollisionHandle = wallCollision.Schedule(wallCollision.AgentMovementData.Length, 64, tensionHandle);

        //Avoidance wall detection
        AvoidanceWallDetectionJob wallDetection = new AvoidanceWallDetectionJob()
        {
            FieldMaxXExcluding = FlowFieldUtilities.FieldMaxXExcluding,
            FieldMaxYExcluding = FlowFieldUtilities.FieldMaxYExcluding,
            FieldMinXIncluding = FlowFieldUtilities.FieldMinXIncluding,
            FieldMinYIncluding = FlowFieldUtilities.FieldMinYIncluding,
            SectorColAmount = FlowFieldUtilities.SectorColAmount,
            SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
            SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
            TileSize = FlowFieldUtilities.TileSize,
            AgentMovementDataArray = AgentMovementDataList,
            CostFieldPerOffset = costFieldCosts,
            RoutineResultArray = RoutineResults,
        };
        JobHandle wallDetectionHandle = wallDetection.Schedule(agentDataArray.Length, 64, wallCollisionHandle);


        PathReachDistances.Length = exposedPathReachDistanceCheckRanges.Length;
        DestinationReachedAgentCountJob destinationReachedCounter = new DestinationReachedAgentCountJob()
        {
            AgentDestinationReachStatus = _agentDataContainer.AgentDestinationReachedArray,
            HashedToNormal = HashedToNormal,
            AgentSpatialHashGrid = new AgentSpatialHashGrid()
            {
                BaseSpatialGridSize = FlowFieldUtilities.BaseAgentSpatialGridSize,
                FieldHorizontalSize = FlowFieldUtilities.TileSize * FlowFieldUtilities.FieldColAmount,
                FieldVerticalSize = FlowFieldUtilities.TileSize * FlowFieldUtilities.FieldRowAmount,
                AgentHashGridArray = HashGridArray,
                RawAgentMovementDataArray = AgentMovementDataList,
            },
            AgentFlockIndicies = agentFlockIndexArray,
            PathDestinationArray = exposedPathDestinationArray,
            PathReachDistances = PathReachDistances,
            PathReachDistanceCheckRanges = exposedPathReachDistanceCheckRanges,
            PathFlockIndexArray = exposedPathFlockIndicies,
            PathStateList = exposedPathStateList,
            PathAgentStopFlagList = exposedPathAgentStopFlagList,
        };
        JobHandle destinationReachedCounterHandle = destinationReachedCounter.Schedule(exposedPathFlockIndicies.Length, 64, wallDetectionHandle);

        AgentStoppingJob stoppingJob = new AgentStoppingJob()
        {
            AgentDestinationReachStatus = _agentDataContainer.AgentDestinationReachedArray,
            AgentCurPathIndicies = agentCurPathIndexArray,
            PathDestinationReachRanges = PathReachDistances,
            AgentMovementDataArray = AgentMovementDataList,
            NormalToHashed = NormalToHashed,
            PathAgentStopFlags = exposedPathAgentStopFlagList,
        };
        JobHandle stoppingHandle = stoppingJob.Schedule(agentDataArray.Length, 64, destinationReachedCounterHandle);

        if (FlowFieldUtilities.DebugMode) { stoppingHandle.Complete(); }
        _routineHandle = stoppingHandle;
    }
    internal void ForceCompleteRoutine()
    {
        _routineHandle.Complete();
    }
    internal void SendRoutineResults()
    {
        TransformAccessArray agentTransforms = _pathfindingManager.AgentDataContainer.AgentTransforms;
        NativeList<bool> agentDestinationReachArray = _pathfindingManager.AgentDataContainer.AgentDestinationReachedArray;
        NativeList<int> agentCurPathIndicies = _pathfindingManager.AgentDataContainer.AgentCurPathIndicies;
        NativeList<AgentData> agentDataArray = _pathfindingManager.AgentDataContainer.AgentDataList;

        AgentPositionChangeSendJob posSendJob = new AgentPositionChangeSendJob()
        {
            AgentPositionChangeBuffer = AgentPositionChangeBuffer,
            NormalToHashed = NormalToHashed,
        };
        posSendJob.Schedule(agentTransforms).Complete();

        RoutineResultSendJob directionSetJob = new RoutineResultSendJob()
        {
            AgentDestinationReachedArray = agentDestinationReachArray,
            AgentCurPathIndicies = agentCurPathIndicies,
            MovementDataArray = AgentMovementDataList,
            AgentDataArray = agentDataArray,
            RoutineResultArray = RoutineResults,
            NormalToHashed = NormalToHashed,
        };
        directionSetJob.Schedule().Complete();

        //MOVE
        AgentMovementUpdateJob movJob = new AgentMovementUpdateJob()
        {
            DeltaTime = Time.fixedDeltaTime,
            AgentDataArray = agentDataArray,
        };
        movJob.Schedule(agentTransforms).Complete();

        //ROTATE
        AgentRotationUpdateJob rotateJob = new AgentRotationUpdateJob()
        {
            agentData = agentDataArray,
        };
        rotateJob.Schedule(agentTransforms).Complete();
    }
}