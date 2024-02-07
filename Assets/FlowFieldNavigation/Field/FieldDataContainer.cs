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
    internal FieldDataContainer(Mesh[] meshes, Transform[] transforms)
    {
        _costFieldProducer = new CostFieldProducer();
        _fieldGraphProducer = new FieldGraphProducer();
        ObstacleContainer = new ObstacleContainer();
        HeightMeshGenerator = new HeightMeshProducer();
        HeightMeshGenerator.GenerateHeightMesh(meshes, transforms);
        NavigationVolumeSystem = new NavigationVolumeSystem();
    }
    internal void CreateField(Walkability[][] walkabilityMatrix, 
        FlowFieldStaticObstacle[] staticObstacles, 
        int maxOffset, float voxelHorizontalSize,
        float voxelVerticalSize, 
        float maxSurfaceHeightDifference,
        float maxWalkableHeight)
    {
        NativeArray<byte> inputCosts = WalkabilityMatrixToCosts(walkabilityMatrix, Allocator.TempJob);
        NativeArray<float3> heightMeshVerts = HeightMeshGenerator.Verticies.AsArray();
        NativeArray<int> heightMeshTrigs = HeightMeshGenerator.Triangles.AsArray();
        NavigationVolumeSystem.AnalyzeVolume(heightMeshVerts, heightMeshTrigs, staticObstacles, voxelHorizontalSize, voxelVerticalSize, maxSurfaceHeightDifference, maxWalkableHeight, inputCosts);
        _costFieldProducer.ProduceCostFields(maxOffset, inputCosts);
        _fieldGraphProducer.ProduceFieldGraphs(_costFieldProducer.GetAllCostFields());
        inputCosts.Dispose();
    }
    NativeArray<byte> WalkabilityMatrixToCosts(Walkability[][] walkabilityMatrix, Allocator allocator)
    {
        NativeArray<byte> costs = new NativeArray<byte>(FlowFieldUtilities.FieldTileAmount, allocator);
        if(walkabilityMatrix == null)
        {
            for(int i = 0; i < costs.Length; i++) { costs[i] = 1; }
            return costs;
        }

        for(int r = 0; r < FlowFieldUtilities.FieldRowAmount; r++)
        {
            for (int c = 0; c < FlowFieldUtilities.FieldColAmount; c++)
            {
                int2 index2 = new int2(c, r);
                int index1 = FlowFieldUtilities.To1D(index2, FlowFieldUtilities.FieldColAmount);
                costs[index1] = walkabilityMatrix[r][c] == Walkability.Unwalkable ? byte.MaxValue : (byte)1;
            }
        }
        return costs;
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