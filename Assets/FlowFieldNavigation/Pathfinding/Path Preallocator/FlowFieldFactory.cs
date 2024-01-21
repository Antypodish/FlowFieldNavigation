using Unity.Collections.LowLevel.Unsafe;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
internal class FlowFieldFactory
{
    List<UnsafeList<FlowData>> _flowFieldContainer;

    internal FlowFieldFactory(int initialSize)
    {
        _flowFieldContainer = new List<UnsafeList<FlowData>>(initialSize);
        for (int i = 0; i < initialSize; i++)
        {
            _flowFieldContainer.Add(new UnsafeList<FlowData>(0, Allocator.Persistent, NativeArrayOptions.ClearMemory));
        }
    }
    internal UnsafeList<FlowData> GetFlowfield(int length)
    {
        if(_flowFieldContainer.Count == 0)
        {
            UnsafeList<FlowData> field = new UnsafeList<FlowData>(length, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            field.Resize(length, NativeArrayOptions.ClearMemory);
            return field;
        }
        else
        {
            UnsafeList<FlowData> field = _flowFieldContainer[0];
            field.Resize(length, NativeArrayOptions.ClearMemory);
            _flowFieldContainer.RemoveAtSwapBack(0);
            return field;
        }
    }
    internal void SendFlowField(UnsafeList<FlowData> flowfield)
    {
        UnsafeListCleaningJob<FlowData> flowData = new UnsafeListCleaningJob<FlowData>()
        {
            List = flowfield,
        };
        flowData.Schedule().Complete();
        _flowFieldContainer.Add(flowfield);
    }
}
