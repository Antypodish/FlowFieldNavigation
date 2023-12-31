using Assets.Path_Finding_System.Scripts;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Jobs;
using System.Diagnostics;
public class MovementIO
{
    AgentDataContainer _agentDataContainer;
    PathfindingManager _pathfindingManager;

    public NativeList<AgentMovementData> AgentMovementDataList;
    public NativeList<float2> AgentPositionChangeBuffer;
    public NativeList<RoutineResult> RoutineResults;
    public NativeList<int> NormalToHashed;
    public NativeList<int> HashedToNormal;
    public NativeArray<UnsafeList<HashTile>> HashGridArray;

    JobHandle _routineHandle;
    public MovementIO(AgentDataContainer agentDataContainer, PathfindingManager pathfindingManager)
    {
        _agentDataContainer = agentDataContainer;
        _pathfindingManager = pathfindingManager;
        AgentMovementDataList = new NativeList<AgentMovementData>(_agentDataContainer.Agents.Count, Allocator.Persistent);
        RoutineResults = new NativeList<RoutineResult>(Allocator.Persistent);
        AgentPositionChangeBuffer = new NativeList<float2>(Allocator.Persistent);
        _routineHandle = new JobHandle();

        //SPATIAL HASH GRID INSTANTIATION
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
    }
    public void ScheduleRoutine(NativeArray<UnsafeListReadOnly<byte>> costFieldCosts, JobHandle dependency)
    {
        NativeArray<AgentData> agentDataArray = _agentDataContainer.AgentDataList;
        NativeArray<int> agentCurPathIndexArray = _agentDataContainer.AgentCurPathIndicies;
        NativeArray<PathLocationData> exposedPathLocationDataArray = _pathfindingManager.PathContainer.ExposedPathLocationData;
        NativeArray<PathFlowData> exposedPathFlowDataArray = _pathfindingManager.PathContainer.ExposedPathFlowData;
        NativeArray<float2> exposedPathDestinationArray = _pathfindingManager.PathContainer.ExposedPathDestinations;
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
            BaseSpatialGridSize = FlowFieldUtilities.BaseSpatialGridSize,
            TileSize = FlowFieldUtilities.TileSize,
            FieldColAmount = FlowFieldUtilities.FieldColAmount,
            FieldRowAmount = FlowFieldUtilities.FieldRowAmount,
            MaxAgentSize = FlowFieldUtilities.MaxAgentSize,
            MinAgentSize = FlowFieldUtilities.MinAgentSize,
            AgentDataArray = agentDataArray,
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
            
            FieldColAmount = _pathfindingManager.ColumnAmount,
            TileSize = _pathfindingManager.TileSize,
            SectorColAmount = _pathfindingManager.SectorColAmount,
            AgentMovementData = AgentMovementDataList,
        };
        JobHandle movDataHandle = routineDataCalcJob.Schedule(routineDataCalcJob.AgentMovementData.Length, 64, spatialHasherHandle);

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
            BaseSpatialGridSize = FlowFieldUtilities.BaseSpatialGridSize,
            FieldHorizontalSize = FlowFieldUtilities.TileSize * FlowFieldUtilities.FieldColAmount,
            FieldVerticalSize = FlowFieldUtilities.TileSize * FlowFieldUtilities.FieldRowAmount,
            AgentMovementDataArray = AgentMovementDataList,
            RoutineResultArray = RoutineResults,
            HashGridArray = HashGridArray,
            SpatialGridUtils = new AgentSpatialGridUtils(0),
            CostFieldEachOffset = costFieldCosts,
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

        //UnityEngine.Debug.Log(sw.Elapsed.TotalMilliseconds);
        if (FlowFieldUtilities.DebugMode) { wallCollisionHandle.Complete(); }
        _routineHandle = wallCollisionHandle;
    }
    public void ForceComplete()
    {
        _routineHandle.Complete();
    }
}