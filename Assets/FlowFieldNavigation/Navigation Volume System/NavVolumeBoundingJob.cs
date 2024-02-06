﻿using Unity.Jobs;
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
    internal int SectorComponentVoxelCount;
    internal float SectorHorizontalSize;
    internal float SectorVerticalSize;
    [ReadOnly] internal NativeArray<float3> Verticies;
    [WriteOnly] internal NativeReference<float3> VolumeStartPos;
    [WriteOnly] internal NativeReference<int> XAxisVoxelCount;
    [WriteOnly] internal NativeReference<int> YAxisVoxelCount;
    [WriteOnly] internal NativeReference<int> ZAxisVoxelCount;

    [WriteOnly] internal NativeReference<int> XAxisSectorCount;
    [WriteOnly] internal NativeReference<int> YAxisSectorCount;
    [WriteOnly] internal NativeReference<int> ZAxisSectorCount;

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

        int xAxisSectorCount = (int)math.ceil(FieldGridSize.x / SectorHorizontalSize);
        int zAxisSectorCount = (int)math.ceil(FieldGridSize.y / SectorHorizontalSize);
        int yAxisSectorCount = (int)math.ceil((yMax - yMin) / SectorVerticalSize + 1);

        int xAxisVoxelCount = xAxisSectorCount * SectorComponentVoxelCount;
        int zAxisVoxelCount = zAxisSectorCount * SectorComponentVoxelCount;
        int yAxisVoxelCount = yAxisSectorCount * SectorComponentVoxelCount;
        VolumeStartPos.Value = volumeStartPos;
        XAxisVoxelCount.Value = xAxisVoxelCount;
        YAxisVoxelCount.Value = yAxisVoxelCount;
        ZAxisVoxelCount.Value = zAxisVoxelCount;
        XAxisSectorCount.Value = xAxisSectorCount;
        YAxisSectorCount.Value = yAxisSectorCount;
        ZAxisSectorCount.Value = zAxisSectorCount;
    }
}
