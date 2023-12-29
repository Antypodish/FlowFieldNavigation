using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Jobs;

public class AgentRoutineDataProducer
{
    AgentDataContainer _agentDataContainer;
    PathfindingManager _pathfindingManager;

    public NativeList<AgentMovementData> AgentMovementDataList;
    public NativeList<float2> AgentPositionChangeBuffer;
    public NativeList<RoutineResult> RoutineResults;
    public NativeArray<UnsafeList<HashTile>> HashGridArray;
    public NativeList<int> NormalToHashed; 
    public AgentRoutineDataProducer(AgentDataContainer agentDataContainer, PathfindingManager pathfindingManager)
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
    }
    public JobHandle PrepareAgentMovementDataCalculationJob(JobHandle dependency)
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
            NormalToHashed = NormalToHashed,
        };
        JobHandle handle = job.Schedule(spatialHasherHandle);

        return handle;
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
public struct myjob : IJob
{
    public float sectorSize;
    public int sectorMatrixColAmount;
    [ReadOnly] public NativeList<AgentData> agentDataList;
    [ReadOnly] public NativeList<int> agentCurPaths;
    [ReadOnly] public NativeList<PathLocationData> exposedPathLocationDataArray;
    [ReadOnly] public NativeList<PathFlowData> exposedPathFlowDataArray;
    [ReadOnly] public NativeList<float2> exposedPathDestinationArray;
    public NativeArray<AgentMovementData> AgentMovementDataList;
    [ReadOnly] public NativeList<int> NormalToHashed;
    public void Execute()
    {
        for (int i = 0; i < agentDataList.Length; i++)
        {
            int agentCurPathIndex = agentCurPaths[i];
            if (agentCurPathIndex == -1) { continue; }
            float2 destination = exposedPathDestinationArray[agentCurPathIndex];
            PathLocationData locationData = exposedPathLocationDataArray[agentCurPathIndex];
            PathFlowData flowData = exposedPathFlowDataArray[agentCurPathIndex];
            int hashedIndex = NormalToHashed[i];
            AgentMovementData data = AgentMovementDataList[hashedIndex];
            data.FlowField = flowData.FlowField;
            data.LOSMap = flowData.LOSMap;
            data.Destination = destination;
            data.SectorFlowStride = locationData.SectorToPicked[FlowFieldUtilities.PosToSector1D(new float2(data.Position.x, data.Position.z), sectorSize, sectorMatrixColAmount)];
            data.PathId = agentCurPathIndex;

            AgentMovementDataList[hashedIndex] = data;
        }
    }
}