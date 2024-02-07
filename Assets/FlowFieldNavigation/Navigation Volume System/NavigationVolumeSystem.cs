using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;
using System.Diagnostics;
internal class NavigationVolumeSystem
{
    internal NativeHashMap<int, UnsafeBitArray> SurfaceVolumeBits;

    internal void GetCostsFromCollisions(NativeArray<float3> navigationSurfaceVerticies, 
        NativeArray<int> navigationSurfaceTriangles,
        FlowFieldStaticObstacle[] staticObstacleBehaviors,
        float voxelHorizontalSize, 
        float voxelVerticalSize,
        NativeArray<byte> costsToWriteOnTopOf)
    {
        float fieldHorizontalSize = FlowFieldUtilities.TileSize * FlowFieldUtilities.FieldColAmount;
        float fieldVerticalSize = FlowFieldUtilities.TileSize * FlowFieldUtilities.FieldRowAmount;
        const int sectorComponentVoxelCount = 10;
        float sectorHorizontalSize = sectorComponentVoxelCount * voxelHorizontalSize;
        float sectorVerticalSize = sectorComponentVoxelCount * voxelVerticalSize;

        NativeArray<StaticObstacle> staticObstacles = GetTransformedStaticObstacles(staticObstacleBehaviors, Allocator.TempJob);

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

        CollidedIndexToCostField collisionToCost = new CollidedIndexToCostField()
        {
            FieldGridStartPos = FlowFieldUtilities.FieldGridStartPosition,
            FieldTileSize = FlowFieldUtilities.TileSize,
            FieldColAmount = FlowFieldUtilities.FieldColAmount,
            FieldRowAmount = FlowFieldUtilities.FieldRowAmount,
            VolStartPos = FlowFieldVolumeUtilities.VolumeStartPos,
            VoxHorSize = FlowFieldVolumeUtilities.VoxelHorizontalSize,
            VoxVerSize = FlowFieldVolumeUtilities.VoxelVerticalSize,
            CollidedIndicies = collidedIndicies,
            Costs = costsToWriteOnTopOf,
        };
        collisionToCost.Schedule().Complete();
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
    NativeArray<StaticObstacle> GetTransformedStaticObstacles(FlowFieldStaticObstacle[] staticObstacles, Allocator allocator)
    {
        NativeArray<StaticObstacle> obstacleOut = new NativeArray<StaticObstacle>(staticObstacles.Length, allocator);
        for(int i = 0; i < staticObstacles.Length; i++)
        {
            FlowFieldStaticObstacle obstacle = staticObstacles[i];
            StaticObstacle inputBounds = obstacle.GetBoundaries();
            Transform inputTransform = obstacle.transform;
            Vector3 lbl = inputBounds.LBL;
            Vector3 ltl = inputBounds.LTL;
            Vector3 ltr = inputBounds.LTR;
            Vector3 lbr = inputBounds.LBR;
            Vector3 ubl = inputBounds.UBL;
            Vector3 utl = inputBounds.UTL;
            Vector3 utr = inputBounds.UTR;
            Vector3 ubr = inputBounds.UBR;

            obstacleOut[i] = new StaticObstacle()
            {
                LBL = inputTransform.TransformPoint(lbl),
                LTL = inputTransform.TransformPoint(ltl),
                LTR = inputTransform.TransformPoint(ltr),
                LBR = inputTransform.TransformPoint(lbr),
                UBL = inputTransform.TransformPoint(ubl),
                UTL = inputTransform.TransformPoint(utl),
                UTR = inputTransform.TransformPoint(utr),
                UBR = inputTransform.TransformPoint(ubr),
            };
            obstacle.CanBeDisposed = true;
        }
        return obstacleOut;
    }
}