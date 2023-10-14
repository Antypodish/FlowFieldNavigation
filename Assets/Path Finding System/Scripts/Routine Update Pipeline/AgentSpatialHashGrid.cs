using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

[BurstCompile]
public struct AgentSpatialHashGrid
{
    public float BaseSpatialGridSize;
    public float FieldHorizontalSize;
    [ReadOnly] public NativeArray<AgentMovementData> AgentMovementDataArray;
    [ReadOnly] public NativeArray<UnsafeList<HashTile>> HashGrids;

    public NativeSlice<AgentMovementData> Get(float2 position, float size)
    {
        int hashGridIndex = (int)math.floor(size / BaseSpatialGridSize);
        float tileSize = hashGridIndex * BaseSpatialGridSize + BaseSpatialGridSize;
        int gridColAmount = (int)math.ceil(FieldHorizontalSize / tileSize);
        int hashTileRow = (int)math.floor(position.y / tileSize);
        int hashTileCol = (int)math.floor(position.x / tileSize);
        int hashTileIndex = hashTileRow * gridColAmount + hashTileCol;
        HashTile tile = HashGrids[hashGridIndex][hashTileIndex];
        return new NativeSlice<AgentMovementData>(AgentMovementDataArray, tile.Start, tile.Length);
    }
}
