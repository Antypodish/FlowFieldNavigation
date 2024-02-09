using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;

internal class FieldDataContainer
{
    internal ObstacleContainer ObstacleContainer { get; private set; }
    internal HeightMeshProducer HeightMeshGenerator { get; private set; }
    internal NavigationVolumeSystem NavigationVolumeSystem { get; private set; }
    CostFieldProducer _costFieldProducer;
    FieldGraphProducer _fieldGraphProducer;
    internal FieldDataContainer(NativeArray<float3> surfaceMeshVerticies, NativeArray<int> surfaceMeshTriangles)
    {
        _costFieldProducer = new CostFieldProducer();
        _fieldGraphProducer = new FieldGraphProducer();
        ObstacleContainer = new ObstacleContainer();
        HeightMeshGenerator = new HeightMeshProducer();
        HeightMeshGenerator.GenerateHeightMesh(surfaceMeshVerticies, surfaceMeshTriangles);
        NavigationVolumeSystem = new NavigationVolumeSystem();
    }
    internal void CreateField(NativeArray<byte> baseCostField, 
        NativeArray<StaticObstacle> staticObstacles, 
        int maxOffset, float voxelHorizontalSize,
        float voxelVerticalSize, 
        float maxSurfaceHeightDifference,
        float maxWalkableHeight)
    {
        NativeArray<float3> heightMeshVerts = HeightMeshGenerator.Verticies.AsArray();
        NativeArray<int> heightMeshTrigs = HeightMeshGenerator.Triangles.AsArray();
        NavigationVolumeSystem.AnalyzeVolume(heightMeshVerts, heightMeshTrigs, staticObstacles, voxelHorizontalSize, voxelVerticalSize, maxSurfaceHeightDifference, maxWalkableHeight, baseCostField);
        _costFieldProducer.ProduceCostFields(maxOffset, baseCostField);
        _fieldGraphProducer.ProduceFieldGraphs(_costFieldProducer.GetAllCostFields());
    }
    internal FieldGraph GetFieldGraphWithOffset(int offset)
    {
        return _fieldGraphProducer.GetFieldGraphWithOffset(offset);
    }
    internal CostField GetCostFieldWithOffset(int offset)
    {
        return _costFieldProducer.GetCostFieldWithOffset(offset);
    }
    internal FieldGraph[] GetAllFieldGraphs()
    {
        return _fieldGraphProducer.GetAllFieldGraphs();
    }
    internal NativeArray<SectorDirectionData> GetSectorDirections()
    {
        return _costFieldProducer.SectorDirections;
    }
    internal NativeArray<IslandFieldProcessor> GetAllIslandFieldProcessors()
    {
        return _fieldGraphProducer.GetAllIslandFieldProcessors();
    }
    internal UnsafeListReadOnly<byte>[] GetAllCostFieldCostsAsUnsafeListReadonly()
    {
        return _costFieldProducer.GetAllCostsAsUnsafeListReadonly();
    }
    internal void DisposeAll()
    {
        ObstacleContainer.DisposeAll();
        HeightMeshGenerator.DisposeAll();
        _costFieldProducer.DisposeAll();
        _fieldGraphProducer.DisposeAll();
    }
}