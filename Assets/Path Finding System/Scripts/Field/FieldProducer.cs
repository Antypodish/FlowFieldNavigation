using System.Runtime.InteropServices.WindowsRuntime;
using Unity.Collections;
using Unity.Jobs;

public class FieldProducer
{
    CostFieldProducer _costFieldProducer;
    FieldGraphProducer _fieldGraphProducer;
    public NativeArray<Edge> _edges;
    public FieldProducer(WalkabilityData walkabilityData, byte sectorTileAmount)
    {
        _costFieldProducer = new CostFieldProducer(walkabilityData, sectorTileAmount);
        _fieldGraphProducer = new FieldGraphProducer();
    }
    public void CreateField(int maxOffset, int sectorColAmount, int sectorMatrixColAmount, int sectorMatrixRowAmount, int fieldRowAmount, int fieldColAmount, float tileSize)
    {
        _costFieldProducer.ProduceCostFields(maxOffset, sectorColAmount, sectorMatrixColAmount, sectorMatrixRowAmount);
        _fieldGraphProducer.ProduceFieldGraphs(_costFieldProducer.GetAllCostFields(), sectorColAmount, fieldRowAmount, fieldColAmount, tileSize);

        _edges = new NativeArray<Edge>(_costFieldProducer.GetCostFieldWithOffset(0).CostsG.Length * 4, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        WallColliderCalculationJob walCalJob = new WallColliderCalculationJob()
        {
            TileSize = tileSize,
            Costs = _costFieldProducer.GetCostFieldWithOffset(0).CostsG,
            FieldColAmount = fieldColAmount,
            FieldRowAmount = fieldRowAmount,
            TileEdges = _edges,
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