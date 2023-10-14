using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.VisualScripting;

[BurstCompile]
public struct AgentDataSpatialHasherJob : IJob
{
    public float TileSize;
    public int FieldRowAmount;
    public int FieldColAmount;
    public float BaseSpatialGridSize;
    public float MinAgentSize;
    public float MaxAgentSize;
    
    [ReadOnly] public NativeArray<AgentData> AgentDataArray;
    public NativeArray<AgentMovementData> AgentMovementDataArray;
    public NativeArray<UnsafeList<HashTile>> AgentHashGridArray;
    [WriteOnly] public NativeArray<int> NormalToHashed;

    public void Execute()
    {
        ClearAgentHashGrid();

        float fieldHorizontalSize = TileSize * FieldColAmount;

        SetHashGridTileSizes(fieldHorizontalSize);
        InsertAgents(fieldHorizontalSize);
    }
    void ClearAgentHashGrid()
    {
        for (int i = 0; i < AgentHashGridArray.Length; i++)
        {
            UnsafeList<HashTile> agentHashGrid = AgentHashGridArray[i];
            for(int j = 0; j < agentHashGrid.Length; j++)
            {
                agentHashGrid[j] = new HashTile()
                {
                    Start = 0,
                    Length = 0,
                };
            }
            
        }
    }
    void SetHashGridTileSizes(float fieldHorizontalSize)
    {
        for (int i = 0; i < AgentDataArray.Length; i++)
        {
            AgentData agentData = AgentDataArray[i];
            float2 pos = new float2(agentData.Position.x, agentData.Position.z);
            int hashGridIndex = (int)math.floor(agentData.Radius * 2 / BaseSpatialGridSize);
            float tileSize = hashGridIndex * BaseSpatialGridSize + BaseSpatialGridSize;
            int gridColAmount = (int)math.ceil(fieldHorizontalSize / tileSize);
            int hashTileRow = (int)math.floor(pos.y / tileSize);
            int hashTileCol = (int)math.floor(pos.x / tileSize);
            int hashTileIndex = hashTileRow * gridColAmount + hashTileCol;
            UnsafeList<HashTile> hashGrid = AgentHashGridArray[hashGridIndex];
            HashTile tile = hashGrid[hashTileIndex];
            tile.Start++;
            hashGrid[hashTileIndex] = tile;
        }

        int totalSize = 0;
        for (int i = 0; i < AgentHashGridArray.Length; i++)
        {
            UnsafeList<HashTile> hashGrid = AgentHashGridArray[i];
            for (int j = 0; j < hashGrid.Length; j++)
            {
                HashTile tile = hashGrid[j];
                int start = tile.Start;
                tile.Start = totalSize;
                totalSize += start;
                hashGrid[j] = tile;
            }
        }
    }
    void InsertAgents(float fieldHorizontalSize)
    {
        for (int i = 0; i < AgentDataArray.Length; i++)
        {
            AgentData agentData = AgentDataArray[i];
            float2 pos = new float2(agentData.Position.x, agentData.Position.z);
            int hashGridIndex = (int)math.floor(agentData.Radius * 2 / BaseSpatialGridSize);
            float tileSize = hashGridIndex * BaseSpatialGridSize + BaseSpatialGridSize;
            int gridColAmount = (int)math.ceil(fieldHorizontalSize / tileSize);
            int hashTileRow = (int)math.floor(pos.y / tileSize);
            int hashTileCol = (int)math.floor(pos.x / tileSize);
            int hashTileIndex = hashTileRow * gridColAmount + hashTileCol;
            UnsafeList<HashTile> hashGrid = AgentHashGridArray[hashGridIndex];
            HashTile tile = hashGrid[hashTileIndex];
            int agentDataIndex = tile.Start + tile.Length;

            AgentMovementDataArray[agentDataIndex] = new AgentMovementData()
            {
                Position = agentData.Position,
                Radius = agentData.Radius,
                Local1d = 0,
                DesiredDirection = 0,
                SeperationForce = 0,
                CurrentDirection = agentData.Direction,
                Speed = agentData.Speed,
                Status = agentData.Status,
                Avoidance = agentData.Avoidance,
                MovingAvoidance = agentData.MovingAvoidance,
                RoutineStatus = 0,
                PathId = -1,
                TensionPowerIndex = -1,
                SplitInfo = agentData.SplitInfo,
                SplitInterval = agentData.SplitInterval,
            };

            tile.Length++;
            hashGrid[hashTileIndex] = tile;
            NormalToHashed[i] = agentDataIndex;
        }
    }
}
public struct HashTile
{
    public int Start;
    public int Length;
}