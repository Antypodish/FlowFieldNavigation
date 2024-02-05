using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;
using Unity.Mathematics;

[BurstCompile]
internal struct NavVolumeBoundingJob : IJob
{
    internal float2 FieldGridStartPos;
    internal float2 FieldGridSize;
    internal float VoxelHorizontalSize;
    internal float VoxelVerticalSize;
    [ReadOnly] internal NativeArray<float3> Verticies;
    [WriteOnly] internal NativeReference<float3> VolumeStartPos;
    [WriteOnly] internal NativeReference<int> XAxisVoxelCount;
    [WriteOnly] internal NativeReference<int> YAxisVoxelCount;
    [WriteOnly] internal NativeReference<int> ZAxisVoxelCount;
    public void Execute()
    {
        float yMin = float.MaxValue;
        float yMax = float.MinValue;
        for(int i = 0; i < Verticies.Length; i++)
        {
            float3 vert = Verticies[i];
            yMin = math.min(yMin, vert.y);
            yMax = math.max(yMax, vert.y);
        }
        float3 volumeStartPos = new float3(FieldGridStartPos.x, yMin, FieldGridStartPos.y);

        int xAxisVoxelCount = (int)math.ceil(FieldGridSize.x / VoxelHorizontalSize);
        int zAxisVoxelCount = (int)math.ceil(FieldGridSize.y / VoxelHorizontalSize);
        int yAxisVoxelCount = (int)math.ceil((yMax - yMin) / VoxelVerticalSize) + 1;
        VolumeStartPos.Value = volumeStartPos;
        XAxisVoxelCount.Value = xAxisVoxelCount;
        YAxisVoxelCount.Value = yAxisVoxelCount;
        ZAxisVoxelCount.Value = zAxisVoxelCount;
    }
}
