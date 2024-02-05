using Unity.Mathematics;
internal static class FlowFieldVolumeUtilities
{
    internal static float3 VolumeStartPos;
    internal static int YAxisVoxelCount;
    internal static int XAxisVoxelCount;
    internal static int ZAxisVoxelCount;
    internal static float VoxelHorizontalSize;
    internal static float VoxelVerticalSize;

    internal static int3 PosToIndex(float3 pos, float3 volumeStartPos, float voxelHorizontalSize, float voxelVerticalSize)
    {
        pos -= volumeStartPos;
        return (int3)math.floor(pos / new float3(voxelHorizontalSize, voxelVerticalSize, voxelHorizontalSize));
    }
    internal static int To1D(int3 index, int xVoxCount, int yVoxCount, int zVoxCount)
    {
        return (index.y * xVoxCount * zVoxCount) + (index.z * xVoxCount) + index.x;
    }
    internal static int3 To3D(int index, int xVoxCount, int yVoxCount, int zVoxCount)
    {
        int y = index / (xVoxCount * zVoxCount);
        int z = index % (xVoxCount * zVoxCount) / xVoxCount;
        int x = index % xVoxCount;
        return new int3(x, y, z);
    }
    internal static int3 Clamp(int3 index, int xVoxCount, int yVoxCount, int zVoxCount)
    {
        index.x = math.max(index.x, 0);
        index.y = math.max(index.y, 0);
        index.z = math.max(index.z, 0);
        index.x = math.min(index.x, xVoxCount - 1);
        index.y = math.min(index.y, yVoxCount - 1);
        index.z = math.min(index.z, zVoxCount - 1);
        return index;
    }
    internal static float3 GetVoxelStartPos(int3 index, float3 volmeStartPos, float voxHorSize, float voxVerSize)
    {
        return volmeStartPos + (index * new float3(voxHorSize, voxVerSize, voxHorSize));
    }
    internal static float3 GetVoxelCenterPos(int3 index, float3 volmeStartPos, float voxHorSize, float voxVerSize)
    {
        return volmeStartPos + (index * new float3(voxHorSize , voxVerSize, voxHorSize)) + new float3(voxHorSize / 2, voxVerSize / 2, voxHorSize / 2);
    }
}
