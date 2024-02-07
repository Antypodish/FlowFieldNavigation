using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

internal struct TileHeightExclusionJob : IJobParallelFor
{
    internal float MaxTileHeight;
    internal float3 VolumeStartPos;
    internal float VoxVerSize;
    [ReadOnly] internal NativeArray<HeightTile> TileHeights;
    [WriteOnly] internal NativeArray<byte> CostField;

    public void Execute(int index)
    {
        HeightTile tile = TileHeights[index];
        int3 tileVoxelIndex = tile.VoxIndex;
        if(tileVoxelIndex.y == int.MinValue)
        {
            CostField[index] = byte.MaxValue;
            return;
        }
        float tileHeight = tile.VoxIndex.y * VoxVerSize + VolumeStartPos.y;
        if(tileHeight > MaxTileHeight)
        {
            CostField[index] = byte.MaxValue;
        }
    }
}