using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;
internal class NavigationVolumeSystem
{
    internal NativeHashMap<int, UnsafeBitArray> SurfaceVolumeBits;

    internal void CalculateVolume(NativeArray<float3> navigationSurfaceVerticies, 
        NativeArray<int> navigationSurfaceTriangles,
        StaticObstacleInput[] staticObstacleInput,
        float voxelHorizontalSize, 
        float voxelVerticalSize)
    {
        float fieldHorizontalSize = FlowFieldUtilities.TileSize * FlowFieldUtilities.FieldColAmount;
        float fieldVerticalSize = FlowFieldUtilities.TileSize * FlowFieldUtilities.FieldRowAmount;
        const int sectorComponentVoxelCount = 10;
        float sectorHorizontalSize = sectorComponentVoxelCount * voxelHorizontalSize;
        float sectorVerticalSize = sectorComponentVoxelCount * voxelVerticalSize;

        NativeArray<StaticObstacle> staticObstacles = GetTransformedStaticObstacles(staticObstacleInput, Allocator.TempJob);

        NativeReference<float3> volumeStartPos = new NativeReference<float3>(0, Allocator.TempJob);
        NativeReference<int> xAxisVoxelCount = new NativeReference<int>(0, Allocator.TempJob);
        NativeReference<int> yAxisVoxelCount = new NativeReference<int>(0, Allocator.TempJob);
        NativeReference<int> zAxisVoxelCount = new NativeReference<int>(0, Allocator.TempJob);
        NativeReference<int> xAxisSectorCount = new NativeReference<int>(0, Allocator.TempJob);
        NativeReference<int> yAxisSectorCount = new NativeReference<int>(0, Allocator.TempJob);
        NativeReference<int> zAxisSectorCount = new NativeReference<int>(0, Allocator.TempJob);
        NavVolumeBoundingJob volumeBounding = new NavVolumeBoundingJob()
        {
            FieldGridSize = new float2(fieldHorizontalSize, fieldVerticalSize),
            FieldGridStartPos = FlowFieldUtilities.FieldGridStartPosition,
            VoxelHorizontalSize = voxelHorizontalSize,
            VoxelVerticalSize = voxelVerticalSize,
            SectorComponentVoxelCount = sectorComponentVoxelCount,
            SectorHorizontalSize = sectorHorizontalSize,
            SectorVerticalSize = sectorVerticalSize,
            Verticies = navigationSurfaceVerticies,
            StaticObstacles = staticObstacles,
            VolumeStartPos = volumeStartPos,
            XAxisVoxelCount = xAxisVoxelCount,
            YAxisVoxelCount = yAxisVoxelCount,
            ZAxisVoxelCount = zAxisVoxelCount,
            XAxisSectorCount = xAxisSectorCount,
            YAxisSectorCount = yAxisSectorCount,
            ZAxisSectorCount = zAxisSectorCount,
        };
        volumeBounding.Schedule().Complete();

        FlowFieldVolumeUtilities.XAxisVoxelCount = xAxisVoxelCount.Value;
        FlowFieldVolumeUtilities.YAxisVoxelCount = yAxisVoxelCount.Value;
        FlowFieldVolumeUtilities.ZAxisVoxelCount = zAxisVoxelCount.Value;
        FlowFieldVolumeUtilities.VolumeStartPos = volumeStartPos.Value;
        FlowFieldVolumeUtilities.VoxelHorizontalSize = voxelHorizontalSize;
        FlowFieldVolumeUtilities.VoxelVerticalSize = voxelVerticalSize;
        FlowFieldVolumeUtilities.XAxisSectorCount = xAxisSectorCount.Value;
        FlowFieldVolumeUtilities.YAxisSectorCount = yAxisSectorCount.Value;
        FlowFieldVolumeUtilities.ZAxisSectorCount = zAxisSectorCount.Value;
        FlowFieldVolumeUtilities.SectorComponentVoxelCount = sectorComponentVoxelCount;
        FlowFieldVolumeUtilities.SectorHorizontalSize = sectorHorizontalSize;
        FlowFieldVolumeUtilities.SectorVerticalSize = sectorVerticalSize;
        NativeHashSet<int> detectedSectorSet = new NativeHashSet<int>(0, Allocator.TempJob);
        NavSurfaceSectorDetectionJob surfaceSectorDetection = new NavSurfaceSectorDetectionJob()
        {
            VolumeStartPos = FlowFieldVolumeUtilities.VolumeStartPos,
            SecHorSize = FlowFieldVolumeUtilities.SectorHorizontalSize,
            SecVerSize = FlowFieldVolumeUtilities.SectorVerticalSize,
            XSecCount = FlowFieldVolumeUtilities.XAxisSectorCount,
            YSecCount = FlowFieldVolumeUtilities.YAxisSectorCount,
            ZSecCount = FlowFieldVolumeUtilities.ZAxisSectorCount,
            Trigs = navigationSurfaceTriangles,
            Verts = navigationSurfaceVerticies,
            SectorIndexSet = detectedSectorSet,
        };
        surfaceSectorDetection.Schedule().Complete();
        SurfaceVolumeBits = AllocateSectors(detectedSectorSet);

        NavSurfaceMarkingJob surfaceMarking = new NavSurfaceMarkingJob()
        {
            VolumeStartPos = FlowFieldVolumeUtilities.VolumeStartPos,
            VoxHorSize = FlowFieldVolumeUtilities.VoxelHorizontalSize,
            VoxVerSize = FlowFieldVolumeUtilities.VoxelVerticalSize,
            XVoxCount = FlowFieldVolumeUtilities.XAxisVoxelCount,
            YVoxCount = FlowFieldVolumeUtilities.YAxisVoxelCount,
            ZVoxCount = FlowFieldVolumeUtilities.ZAxisVoxelCount,
            SectorCompVoxCount = FlowFieldVolumeUtilities.SectorComponentVoxelCount,
            XSecCount = FlowFieldVolumeUtilities.XAxisSectorCount,
            ZSecCount = FlowFieldVolumeUtilities.ZAxisSectorCount,
            Trigs = navigationSurfaceTriangles,
            Verts = navigationSurfaceVerticies,
            SectorBits = SurfaceVolumeBits,
        };
        surfaceMarking.Schedule().Complete();

        NativeList<int3> collidedIndicies = new NativeList<int3>(Allocator.TempJob);
        NavObstacleDetectionJob obstacleDetection = new NavObstacleDetectionJob()
        {
            SecCompVoxCount = FlowFieldVolumeUtilities.SectorComponentVoxelCount,
            VolStartPos = FlowFieldVolumeUtilities.VolumeStartPos,
            VoxHorSize = FlowFieldVolumeUtilities.VoxelHorizontalSize,
            VoxVerSize = FlowFieldVolumeUtilities.VoxelVerticalSize,
            XSecCount = FlowFieldVolumeUtilities.XAxisSectorCount,
            ZSecCount = FlowFieldVolumeUtilities.ZAxisSectorCount,
            XVoxCount = FlowFieldVolumeUtilities.XAxisVoxelCount,
            YVoxCount = FlowFieldVolumeUtilities.YAxisVoxelCount,
            ZVoxCount = FlowFieldVolumeUtilities.ZAxisVoxelCount,
            SurfaceVolumeBits = SurfaceVolumeBits,
            StaticObstacles = staticObstacles,
            CollidedIndicies = collidedIndicies,
        };
        obstacleDetection.Schedule().Complete();
    }
    NativeHashMap<int, UnsafeBitArray> AllocateSectors(NativeHashSet<int> sectorToAllocate)
    {
        int sectorComponentVoxelCount = FlowFieldVolumeUtilities.SectorComponentVoxelCount;
        int sectorVoxelCount = sectorComponentVoxelCount * sectorComponentVoxelCount * sectorComponentVoxelCount;
        NativeHashMap<int, UnsafeBitArray> volumeBits = new NativeHashMap<int, UnsafeBitArray>(0, Allocator.Persistent);

        NativeHashSet<int>.Enumerator setEnumerator = sectorToAllocate.GetEnumerator();
        while (setEnumerator.MoveNext())
        {
            int curSector = setEnumerator.Current;
            UnsafeBitArray bitArray = new UnsafeBitArray(sectorVoxelCount, Allocator.Persistent);
            volumeBits.Add(curSector, bitArray);
        }
        return volumeBits;
    }
    NativeArray<StaticObstacle> GetTransformedStaticObstacles(StaticObstacleInput[] staticObstacleInput, Allocator allocator)
    {
        NativeArray<StaticObstacle> obstacleOut = new NativeArray<StaticObstacle>(staticObstacleInput.Length, allocator);
        for(int i = 0; i <staticObstacleInput.Length; i++)
        {
            StaticObstacleInput input = staticObstacleInput[i];
            Vector3 lbl = input.Obstacle.LBL;
            Vector3 ltl = input.Obstacle.LTL;
            Vector3 ltr = input.Obstacle.LTR;
            Vector3 lbr = input.Obstacle.LBR;
            Vector3 ubl = input.Obstacle.UBL;
            Vector3 utl = input.Obstacle.UTL;
            Vector3 utr = input.Obstacle.UTR;
            Vector3 ubr = input.Obstacle.UBR;

            obstacleOut[i] = new StaticObstacle()
            {
                LBL = input.Transform.TransformPoint(lbl),
                LTL = input.Transform.TransformPoint(ltl),
                LTR = input.Transform.TransformPoint(ltr),
                LBR = input.Transform.TransformPoint(lbr),
                UBL = input.Transform.TransformPoint(ubl),
                UTL = input.Transform.TransformPoint(utl),
                UTR = input.Transform.TransformPoint(utr),
                UBR = input.Transform.TransformPoint(ubr),
            };
        }
        return obstacleOut;
    }
}