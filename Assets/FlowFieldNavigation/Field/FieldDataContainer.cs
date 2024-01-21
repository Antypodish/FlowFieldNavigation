using UnityEngine;
using Unity.Collections;

public class FieldDataContainer
{
    public ObstacleContainer ObstacleContainer { get; private set; }
    public HeightMeshProducer HeightMeshGenerator { get; private set; }
    CostFieldProducer _costFieldProducer;
    FieldGraphProducer _fieldGraphProducer;
    public FieldDataContainer(WalkabilityCell[][] walkabilityMatrix, Mesh[] meshes, Transform[] transforms)
    {
        _costFieldProducer = new CostFieldProducer(walkabilityMatrix);
        _fieldGraphProducer = new FieldGraphProducer();
        ObstacleContainer = new ObstacleContainer();
        HeightMeshGenerator = new HeightMeshProducer();
        HeightMeshGenerator.GenerateHeightMesh(meshes, transforms);
    }
    public void CreateField(int maxOffset)
    {
        _costFieldProducer.ProduceCostFields(maxOffset);
        _fieldGraphProducer.ProduceFieldGraphs(_costFieldProducer.GetAllCostFields());
    }
    public FieldGraph GetFieldGraphWithOffset(int offset)
    {
        return _fieldGraphProducer.GetFieldGraphWithOffset(offset);
    }
    public CostField GetCostFieldWithOffset(int offset)
    {
        return _costFieldProducer.GetCostFieldWithOffset(offset);
    }
    public FieldGraph[] GetAllFieldGraphs()
    {
        return _fieldGraphProducer.GetAllFieldGraphs();
    }
    public NativeArray<SectorDirectionData> GetSectorDirections()
    {
        return _costFieldProducer.SectorDirections;
    }
    public NativeArray<IslandFieldProcessor> GetAllIslandFieldProcessors()
    {
        return _fieldGraphProducer.GetAllIslandFieldProcessors();
    }
    public UnsafeListReadOnly<byte>[] GetAllCostFieldCostsAsUnsafeListReadonly()
    {
        return _costFieldProducer.GetAllCostsAsUnsafeListReadonly();
    }
}