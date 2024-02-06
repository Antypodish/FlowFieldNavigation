using UnityEngine;
using Unity.Collections;

internal class FieldDataContainer
{
    internal ObstacleContainer ObstacleContainer { get; private set; }
    internal HeightMeshProducer HeightMeshGenerator { get; private set; }
    internal NavigationVolumeSystem NavigationVolumeSystem { get; private set; }
    CostFieldProducer _costFieldProducer;
    FieldGraphProducer _fieldGraphProducer;
    internal FieldDataContainer(Walkability[][] walkabilityMatrix, Mesh[] meshes, Transform[] transforms, float voxelHorizontalSize, float voxelVerticalSize)
    {
        _costFieldProducer = new CostFieldProducer(walkabilityMatrix);
        _fieldGraphProducer = new FieldGraphProducer();
        ObstacleContainer = new ObstacleContainer();
        HeightMeshGenerator = new HeightMeshProducer();
        HeightMeshGenerator.GenerateHeightMesh(meshes, transforms);
        NavigationVolumeSystem = new NavigationVolumeSystem();
        NavigationVolumeSystem.CalculateVolume(HeightMeshGenerator.Verticies.AsArray(), HeightMeshGenerator.Triangles.AsArray(), new StaticObstacleInput[0], voxelHorizontalSize, voxelVerticalSize);
    }
    internal void CreateField(int maxOffset)
    {
        _costFieldProducer.ProduceCostFields(maxOffset);
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