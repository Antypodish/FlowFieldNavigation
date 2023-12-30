﻿using Assets.Path_Finding_System.Scripts;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Jobs;

public class MovementIO
{
    AgentDataContainer _agentDataContainer;
    PathfindingManager _pathfindingManager;

    public NativeList<AgentMovementData> AgentMovementDataList;
    public NativeList<float2> AgentPositionChangeBuffer;
    public NativeList<RoutineResult> RoutineResults;
    public NativeArray<UnsafeList<HashTile>> HashGridArray;
    public NativeList<int> NormalToHashed;
    public NativeList<int> HashedToNormal;

    JobHandle _routineHandle;
    public MovementIO(AgentDataContainer agentDataContainer, PathfindingManager pathfindingManager)
    {
        _agentDataContainer = agentDataContainer;
        _pathfindingManager = pathfindingManager;
        AgentMovementDataList = new NativeList<AgentMovementData>(_agentDataContainer.Agents.Count, Allocator.Persistent);
        RoutineResults = new NativeList<RoutineResult>(Allocator.Persistent);
        AgentPositionChangeBuffer = new NativeList<float2>(Allocator.Persistent);
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
        HashedToNormal = new NativeList<int>(Allocator.Persistent);
        _routineHandle = new JobHandle();
    }
    public void ScheduleRoutine(NativeArray<UnsafeListReadOnly<byte>> costFieldCosts, JobHandle dependency)
    {
        NativeList<AgentData> agentDataList = _agentDataContainer.AgentDataList;
        NativeList<int> agentCurPaths = _agentDataContainer.AgentCurPathIndicies;
        NativeList<PathLocationData> exposedPathLocationDataArray = _pathfindingManager.PathContainer.ExposedPathLocationData;
        NativeList<PathFlowData> exposedPathFlowDataArray = _pathfindingManager.PathContainer.ExposedPathFlowData;
        NativeList<float2> exposedPathDestinationArray = _pathfindingManager.PathContainer.ExposedPathDestinations;
        //CLEAR
        AgentMovementDataList.Clear();
        AgentPositionChangeBuffer.Clear();
        RoutineResults.Clear();
        NormalToHashed.Clear();
        AgentMovementDataList.Length = agentDataList.Length;
        RoutineResults.Length = agentDataList.Length;
        AgentPositionChangeBuffer.Length = agentDataList.Length;
        NormalToHashed.Length = agentDataList.Length;
        HashedToNormal.Length = agentDataList.Length;

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
            HashedToNormal = HashedToNormal,
        };
        JobHandle spatialHasherHandle = spatialHasher.Schedule(dependency);

        //FILL AGENT MOVEMENT DATA ARRAY
        float sectorSize = FlowFieldUtilities.SectorColAmount * FlowFieldUtilities.TileSize;
        int sectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount;
        myjob job = new myjob()
        {
            sectorMatrixColAmount = sectorMatrixColAmount,
            sectorSize = sectorSize,
            agentCurPaths = agentCurPaths,
            agentDataList = agentDataList,
            AgentMovementDataList = AgentMovementDataList,
            exposedPathDestinationArray = exposedPathDestinationArray,
            exposedPathFlowDataArray = exposedPathFlowDataArray,
            exposedPathLocationDataArray = exposedPathLocationDataArray,
            HashedToNormal = HashedToNormal,
        };
        JobHandle handle = job.Schedule(agentDataList.Length, 64, spatialHasherHandle);

        AgentRoutineDataCalculationJob movDataJob = GetAgentMovementDataCalcJob();
        JobHandle movDataHandle = movDataJob.Schedule(movDataJob.AgentMovementData.Length, 64, handle);
        //SCHEDULE AGENT COLLISION JOB
        CollisionResolutionJob colResJob = new CollisionResolutionJob()
        {
            AgentMovementDataArray = AgentMovementDataList,
            AgentPositionChangeBuffer = AgentPositionChangeBuffer,
            HashGridArray = HashGridArray,
            SpatialGridUtils = new AgentSpatialGridUtils(0),
        };
        JobHandle colResHandle = colResJob.Schedule(colResJob.AgentMovementDataArray.Length, 4, movDataHandle);

        //SCHEDULE LOCAL AVODANCE JOB
        LocalAvoidanceJob avoidanceJob = new LocalAvoidanceJob()
        {
            SeperationMultiplier = BoidController.Instance.SeperationMultiplier,
            SeperationRangeAddition = BoidController.Instance.SeperationRangeAddition,
            SeekMultiplier = BoidController.Instance.SeekMultiplier,
            AlignmentMultiplier = BoidController.Instance.AlignmentMultiplier,
            AlignmentRangeAddition = BoidController.Instance.AlignmentRangeAddition,
            MovingAvoidanceRangeAddition = BoidController.Instance.MovingAvoidanceRangeAddition,
            BaseSpatialGridSize = FlowFieldUtilities.BaseSpatialGridSize,
            FieldHorizontalSize = FlowFieldUtilities.TileSize * FlowFieldUtilities.FieldColAmount,
            FieldVerticalSize = FlowFieldUtilities.TileSize * FlowFieldUtilities.FieldRowAmount,
            AgentMovementDataArray = AgentMovementDataList,
            RoutineResultArray = RoutineResults,
            HashGridArray = HashGridArray,
            SpatialGridUtils = new AgentSpatialGridUtils(0),
        };
        JobHandle avoidanceHandle = avoidanceJob.Schedule(avoidanceJob.AgentMovementDataArray.Length, 64, colResHandle);

        //SCHEDULE TENSON RES JOB
        TensionResolver tensionResJob = new TensionResolver()
        {
            HashGridArray = HashGridArray,
            HashGridUtils = new AgentSpatialGridUtils(0),
            RoutineResultArray = RoutineResults,
            AgentMovementDataArray = AgentMovementDataList,
            SeperationRangeAddition = BoidController.Instance.SeperationRangeAddition,
        };
        JobHandle tensionHandle = tensionResJob.Schedule(avoidanceHandle);

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
        if (FlowFieldUtilities.DebugMode) { wallCollisionHandle.Complete(); }
        _routineHandle = wallCollisionHandle;
    }
    public void ForceComplete()
    {
        _routineHandle.Complete();
    }
    public AgentRoutineDataCalculationJob GetAgentMovementDataCalcJob()
    {
        return new AgentRoutineDataCalculationJob()
        {
            FieldColAmount = _pathfindingManager.ColumnAmount,
            TileSize = _pathfindingManager.TileSize,
            SectorColAmount = _pathfindingManager.SectorColAmount,
            SectorMatrixColAmount = _pathfindingManager.SectorMatrixColAmount,
            AgentMovementData = AgentMovementDataList,
        };
    }
}

[BurstCompile]
public struct myjob : IJobParallelFor
{
    public float sectorSize;
    public int sectorMatrixColAmount;
    [ReadOnly] public NativeList<AgentData> agentDataList;
    [ReadOnly] public NativeList<int> agentCurPaths;
    [ReadOnly] public NativeList<PathLocationData> exposedPathLocationDataArray;
    [ReadOnly] public NativeList<PathFlowData> exposedPathFlowDataArray;
    [ReadOnly] public NativeList<float2> exposedPathDestinationArray;
    [ReadOnly] public NativeList<int> HashedToNormal;

    public NativeArray<AgentMovementData> AgentMovementDataList;
    public void Execute(int index)
    {
        int normalIndex = HashedToNormal[index];
        int agentCurPathIndex = agentCurPaths[normalIndex];
        if (agentCurPathIndex == -1) { return; }
        float2 destination = exposedPathDestinationArray[agentCurPathIndex];
        PathLocationData locationData = exposedPathLocationDataArray[agentCurPathIndex];
        PathFlowData flowData = exposedPathFlowDataArray[agentCurPathIndex];
        AgentMovementData data = AgentMovementDataList[index];
        data.FlowField = flowData.FlowField;
        data.LOSMap = flowData.LOSMap;
        data.Destination = destination;
        data.SectorFlowStride = locationData.SectorToPicked[FlowFieldUtilities.PosToSector1D(new float2(data.Position.x, data.Position.z), sectorSize, sectorMatrixColAmount)];
        data.PathId = agentCurPathIndex;

        AgentMovementDataList[index] = data;
    }
}