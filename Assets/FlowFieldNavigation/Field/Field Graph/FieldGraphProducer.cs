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
            _fieldGraphs[i] = new FieldGraph(sectorColAmount, fieldRowAmount, fieldColAmount, i, tileSize);
        }

        //CONFIGURE FIELD GRAPHS
        //field graph config job is weird. It breaks when you want to schedule
        //field graph config job of multiple fields. No unsafe stuff used. Weird.
        NativeArray<JobHandle> combinedHandles = new NativeArray<JobHandle>(_fieldGraphs.Length, Allocator.Temp, NativeArrayOptions.ClearMemory);
        for (int i = 0; i < _fieldGraphs.Length; i++)
        {
            FieldGraphConfigurationJob _fieldGraphConfigJob = _fieldGraphs[i].GetConfigJob(costFields[i].Costs);
            IslandConfigurationJob islandConfigJob = _fieldGraphs[i].GetIslandConfigJob(costFields[i].Costs);
            JobHandle fieldHandle = _fieldGraphConfigJob.Schedule();
            JobHandle islandHandle = islandConfigJob.Schedule(fieldHandle);
            islandHandle.Complete();
            combinedHandles[i] = islandHandle;
        }
        //JobHandle.CompleteAll(combinedHandles);
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
