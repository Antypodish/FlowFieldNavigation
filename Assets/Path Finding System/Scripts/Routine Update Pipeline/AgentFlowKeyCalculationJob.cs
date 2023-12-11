using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

[BurstCompile]
public struct AgentFlowKeyCalculationJob : IJobParallelFor
{
    [WriteOnly] public NativeArray<int> AgentFlowFieldKeys;
    [ReadOnly] public NativeArray<int> AgentCurrentPathIndicies;
    [ReadOnly] public NativeArray<UnsafeList<int>> SectorFlowStarts;
    [ReadOnly] public NativeArray<UnsafeLOSBitmap> LOSBitmaps;
    [ReadOnly] public NativeArray<UnsafeList<SectorFlowStart>> DynamicAreaSectorFlowStarts;
    public void Execute(int index)
    {
        int agentCurPathIndex = AgentCurrentPathIndicies[index];
        if(index == -1) { return; }NativeSlice<int> a;
    }
}
