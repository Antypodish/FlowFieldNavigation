using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using System.Diagnostics;
using UnityEngine;

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
            IslandConfigurationJob islandConfigJob = _fieldGraphs[i].GetIslandConfigJob();
            JobHandle fieldHandle = _fieldGraphConfigJob.Schedule();
            combinedHandles[i] = islandConfigJob.Schedule(fieldHandle);
        }
        JobHandle.CompleteAll(combinedHandles);
    }
    public CostFieldEditJob[] GetEditJobs(NativeArray<CostEditRequest>.ReadOnly costEditRequests)
    {
        CostFieldEditJob[] editJobs = new CostFieldEditJob[_fieldGraphs.Length];
        for (int i = 0; i < editJobs.Length; i++)
        {
            editJobs[i] = _fieldGraphs[i].GetEditJob(costEditRequests);
        }
        return editJobs;
    }
    public IslandReconfigurationJob[] GetIslandReconfigJobs()
    {
        IslandReconfigurationJob[] editJobs = new IslandReconfigurationJob[_fieldGraphs.Length];
        for (int i = 0; i < _fieldGraphs.Length; i++)
        {
            editJobs[i] = _fieldGraphs[i].GetIslandReconfigJob(i);
        }
        return editJobs;
    }
    public FieldGraph GetFieldGraphWithOffset(int offset)
    {
        return _fieldGraphs[offset];
    }
    public FieldGraph[] GetAllFieldGraphs()
    {
        FieldGraph[] newarray = new FieldGraph[_fieldGraphs.Length];
        for(int i = 0; i <newarray.Length; i++)
        {
            newarray[i] = _fieldGraphs[i];
        }
        return newarray;
    }
    public NativeArray<IslandFieldProcessor> GetAllIslandFieldProcessors()
    {
        NativeArray<IslandFieldProcessor> islandFieldProcessors = new NativeArray<IslandFieldProcessor>(_fieldGraphs.Length, Allocator.Persistent);
        for(int i = 0; i < _fieldGraphs.Length; i++)
        {
            islandFieldProcessors[i] = _fieldGraphs[i].GetIslandFieldProcessor();
        }
        return islandFieldProcessors;
    }
}
