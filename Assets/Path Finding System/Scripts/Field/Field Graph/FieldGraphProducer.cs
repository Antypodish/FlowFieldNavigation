using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

public class FieldGraphProducer
{
    FieldGraph[] _fieldGraphs;
    
    public FieldGraphProducer() { }
    public void ProduceFieldGraphs(CostField[] costFields, int sectorColAmount, int fieldRowAmount, int fieldColAmount, float tileSize)
    {
        //ALLOCATE FIELD GRAPHS
        _fieldGraphs = new FieldGraph[costFields.Length];
        for (int i = 0; i < _fieldGraphs.Length; i++)
        {
            _fieldGraphs[i] = new FieldGraph(costFields[i].CostsG, costFields[i].CostsL, sectorColAmount, fieldRowAmount, fieldColAmount, i, tileSize);
        }

        //CONFIGURE FIELD GRAPHS
        NativeArray<JobHandle> combinedHandles = new NativeArray<JobHandle>(_fieldGraphs.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        for (int i = 0; i < _fieldGraphs.Length; i++)
        {
            FieldGraphConfigurationJob _fieldGraphConfigJob = _fieldGraphs[i].GetConfigJob();
            combinedHandles[i] = _fieldGraphConfigJob.Schedule();
        }
        JobHandle.CompleteAll(combinedHandles);
    }
    public CostFieldEditJob[] GetEditJobs(BoundaryData bounds, byte newCost)
    {
        CostFieldEditJob[] editJobs = new CostFieldEditJob[_fieldGraphs.Length];
        for (int i = 0; i < editJobs.Length; i++)
        {
            editJobs[i] = _fieldGraphs[i].GetEditJob(bounds, newCost);
        }
        return editJobs;
    }
    public FieldGraph GetFieldGraphWithOffset(int offset)
    {
        return _fieldGraphs[offset];
    }
    public FieldGraph[] GetAllFieldGraphs()
    {
        return _fieldGraphs;
    }
}
