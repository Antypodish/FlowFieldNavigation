using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.VisualScripting;

namespace FlowFieldNavigation
{

    [BurstCompile]
    internal struct AgentDataSpatialHasherJob : IJob
    {
        internal float TileSize;
        internal int FieldRowAmount;
        internal int FieldColAmount;
        internal float BaseSpatialGridSize;
        internal float MinAgentSize;
        internal float MaxAgentSize;
        internal float2 FieldGridStartPos;

        [ReadOnly] internal NativeArray<int> AgentFlockIndexArray;
        [ReadOnly] internal NativeArray<AgentData> AgentDataArray;
        internal NativeArray<AgentMovementData> AgentMovementDataArray;
        internal NativeArray<UnsafeList<HashTile>> AgentHashGridArray;
        [WriteOnly] internal NativeArray<int> NormalToHashed;
        [WriteOnly] internal NativeArray<int> HashedToNormal;

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
                for (int j = 0; j < agentHashGrid.Length; j++)
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
                int2 hashTileIndex2 = FlowFieldUtilities.PosTo2D(pos, tileSize, FieldGridStartPos);
                int hashTileRow = hashTileIndex2.y;
                int hashTileCol = hashTileIndex2.x;
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
                int2 hashTileIndex2 = FlowFieldUtilities.PosTo2D(pos, tileSize, FieldGridStartPos);
                int hashTileRow = hashTileIndex2.y;
                int hashTileCol = hashTileIndex2.x;
                int hashTileIndex = hashTileRow * gridColAmount + hashTileCol;
                UnsafeList<HashTile> hashGrid = AgentHashGridArray[hashGridIndex];
                HashTile tile = hashGrid[hashTileIndex];
                int agentDataIndex = tile.Start + tile.Length;

                AgentMovementDataArray[agentDataIndex] = new AgentMovementData()
                {
                    Position = agentData.Position,
                    Radius = agentData.Radius,
                    DesiredDirection = agentData.DesiredDirection,
                    AlignmentMultiplierPercentage = 1f,
                    CurrentDirection = agentData.Direction,
                    Speed = agentData.Speed,
                    Status = agentData.Status,
                    LandOffset = agentData.LandOffset,
                    Avoidance = agentData.Avoidance,
                    MovingAvoidance = agentData.MovingAvoidance,
                    RoutineStatus = 0,
                    PathId = -1,
                    TensionPowerIndex = -1,
                    SplitInfo = agentData.SplitInfo,
                    SplitInterval = agentData.SplitInterval,
                    FlockIndex = AgentFlockIndexArray[i],
                };

                tile.Length++;
                hashGrid[hashTileIndex] = tile;
                NormalToHashed[i] = agentDataIndex;
                HashedToNormal[agentDataIndex] = i;
            }
        }
    }
    internal struct HashTile
    {
        internal int Start;
        internal int Length;
    }

}