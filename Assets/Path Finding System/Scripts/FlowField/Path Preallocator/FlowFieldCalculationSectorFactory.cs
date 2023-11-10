using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Jobs;

public class FlowFieldCalculationSectorFactory
{
    List<UnsafeList<FlowData>> _flowFieldContainer;

    public FlowFieldCalculationSectorFactory(int initialSize)
    {
        _flowFieldContainer = new List<UnsafeList<FlowData>>(initialSize);
        for (int i = 0; i < initialSize; i++)
        {
            _flowFieldContainer.Add(new UnsafeList<FlowData>(0, Allocator.Persistent, NativeArrayOptions.ClearMemory));
        }
    }
    public UnsafeList<FlowData> GetFlowfield(int sectorTileAmount)
    {
        if (_flowFieldContainer.Count == 0)
        {
            UnsafeList<FlowData> field = new UnsafeList<FlowData>(sectorTileAmount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            field.Length = sectorTileAmount;
            return field;
        }
        else
        {
            UnsafeList<FlowData> field = _flowFieldContainer[0];
            field.Length = sectorTileAmount;
            _flowFieldContainer.RemoveAtSwapBack(0);
            return field;
        }
    }
    public void SendFlowField(UnsafeList<FlowData> flowfield)
    {
        UnsafeListCleaningJob<FlowData> flowData = new UnsafeListCleaningJob<FlowData>()
        {
            List = flowfield,
        };
        flowData.Schedule().Complete();
        _flowFieldContainer.Add(flowfield);
    }
}
