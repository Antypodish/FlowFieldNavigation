using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
internal class NavigationVolumeSystem
{
    internal NativeBitArray NavigationVolumeBitArray;

    internal void CalculateVolume(NativeArray<float3> navigationSurfaceVerticies, NativeArray<int> navigationSurfaceTriangles, float voxelHorizontalSize, float voxelVerticalSize)
    {
        float fieldHorizontalSize = FlowFieldUtilities.TileSize * FlowFieldUtilities.FieldColAmount;
        float fieldVerticalSize = FlowFieldUtilities.TileSize * FlowFieldUtilities.FieldRowAmount;

        NativeReference<float3> volumeStartPos = new NativeReference<float3>(0, Allocator.TempJob);
        NativeReference<int> xAxisVoxelCount = new NativeReference<int>(0, Allocator.TempJob);
        NativeReference<int> yAxisVoxelCount = new NativeReference<int>(0, Allocator.TempJob);
        NativeReference<int> zAxisVoxelCount = new NativeReference<int>(0, Allocator.TempJob);
        NavVolumeBoundingJob volumeBounding = new NavVolumeBoundingJob()
        {
            FieldGridSize = new float2(fieldHorizontalSize, fieldVerticalSize),
            FieldGridStartPos = FlowFieldUtilities.FieldGridStartPosition,
            VoxelHorizontalSize = voxelHorizontalSize,
            VoxelVerticalSize = voxelVerticalSize,
            Verticies = navigationSurfaceVerticies,
            VolumeStartPos = volumeStartPos,
            XAxisVoxelCount = xAxisVoxelCount,
            YAxisVoxelCount = yAxisVoxelCount,
            ZAxisVoxelCount = zAxisVoxelCount,
        };
        volumeBounding.Schedule().Complete();

        FlowFieldVolumeUtilities.XAxisVoxelCount = xAxisVoxelCount.Value;
        FlowFieldVolumeUtilities.YAxisVoxelCount = yAxisVoxelCount.Value;
        FlowFieldVolumeUtilities.ZAxisVoxelCount = zAxisVoxelCount.Value;
        FlowFieldVolumeUtilities.VolumeStartPos = volumeStartPos.Value;
        FlowFieldVolumeUtilities.VoxelHorizontalSize = voxelHorizontalSize;
        FlowFieldVolumeUtilities.VoxelVerticalSize = voxelVerticalSize;

        NavigationVolumeBitArray = new NativeBitArray(xAxisVoxelCount.Value * yAxisVoxelCount.Value * zAxisVoxelCount.Value, Allocator.Persistent);
        NavSurfaceMarkingJob surfaceMarking = new NavSurfaceMarkingJob()
        {
            VolumeStartPos = FlowFieldVolumeUtilities.VolumeStartPos,
            VoxHorSize = FlowFieldVolumeUtilities.VoxelHorizontalSize,
            VoxVerSize = FlowFieldVolumeUtilities.VoxelVerticalSize,
            XVoxCount = FlowFieldVolumeUtilities.XAxisVoxelCount,
            YVoxCount = FlowFieldVolumeUtilities.YAxisVoxelCount,
            ZVoxCount = FlowFieldVolumeUtilities.ZAxisVoxelCount,
            Trigs = navigationSurfaceTriangles,
            Verts = navigationSurfaceVerticies,
            VolumeMarks = NavigationVolumeBitArray,
        };
        surfaceMarking.Schedule().Complete();
    }
}
