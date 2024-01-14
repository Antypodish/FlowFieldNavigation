using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

public class FieldProducer
{
    CostFieldProducer _costFieldProducer;
    FieldGraphProducer _fieldGraphProducer;
    public FieldProducer(WalkabilityCell[][] walkabilityMatrix, byte sectorTileAmount)
    {
        _costFieldProducer = new CostFieldProducer(walkabilityMatrix, sectorTileAmount);
        _fieldGraphProducer = new FieldGraphProducer();
    }
    public void CreateField(int maxOffset, int sectorColAmount, int sectorMatrixColAmount, int sectorMatrixRowAmount, int fieldRowAmount, int fieldColAmount, float tileSize)
    {
        _costFieldProducer.ProduceCostFields(maxOffset, fieldRowAmount, fieldColAmount, sectorColAmount, sectorMatrixColAmount, sectorMatrixRowAmount);
        _fieldGraphProducer.ProduceFieldGraphs(_costFieldProducer.GetAllCostFields(), sectorColAmount, fieldRowAmount, fieldColAmount, tileSize);
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
    public CostField[] GetAllCostFields()
    {
        return _costFieldProducer.GetAllCostFields();
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