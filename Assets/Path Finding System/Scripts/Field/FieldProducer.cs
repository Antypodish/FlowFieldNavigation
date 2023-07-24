using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using TMPro;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

public class FieldProducer
{
    CostFieldProducer _costFieldProducer;
    FieldGraphProducer _fieldGraphProducer;
    public NativeArray<int> TileToWallObject;
    public NativeList<float2> VertexSequence;
    public NativeList<WallObject> WallObjectList;
    public FieldProducer(WalkabilityData walkabilityData, byte sectorTileAmount)
    {
        _costFieldProducer = new CostFieldProducer(walkabilityData, sectorTileAmount);
        _fieldGraphProducer = new FieldGraphProducer();
    }
    public void CreateField(int maxOffset, int sectorColAmount, int sectorMatrixColAmount, int sectorMatrixRowAmount, int fieldRowAmount, int fieldColAmount, float tileSize)
    {
        _costFieldProducer.ProduceCostFields(maxOffset, sectorColAmount, sectorMatrixColAmount, sectorMatrixRowAmount);
        _fieldGraphProducer.ProduceFieldGraphs(_costFieldProducer.GetAllCostFields(), sectorColAmount, fieldRowAmount, fieldColAmount, tileSize);

        TileToWallObject = new NativeArray<int>(_costFieldProducer.GetCostFieldWithOffset(0).CostsG.Length, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        VertexSequence = new NativeList<float2>(Allocator.Persistent);
        WallObjectList = new NativeList<WallObject>(Allocator.Persistent);
        WallColliderCalculationJob walCalJob = new WallColliderCalculationJob()
        {
            WallObjectList = WallObjectList,
            TileToWallObject = TileToWallObject,
            VertexSequence = VertexSequence,
            TileSize = tileSize,
            Costs = _costFieldProducer.GetCostFieldWithOffset(0).CostsG,
            FieldColAmount = fieldColAmount,
            FieldRowAmount = fieldRowAmount,
        };
        walCalJob.Schedule().Complete();
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
    public CostFieldEditJob[] GetCostFieldEditJobs(BoundaryData bounds, byte newCost)
    {
        return _fieldGraphProducer.GetEditJobs(bounds, newCost);
    }
}