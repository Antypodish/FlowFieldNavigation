using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

[BurstCompile]
public struct FlowFieldExtensionJob : IJobParallelFor
{
    public int oldFieldLength;

    [ReadOnly] public NativeArray<FlowData> OldFlowField;
    [ReadOnly] public NativeArray<IntegrationTile> OldIntegrationField;
    
    [WriteOnly] public NativeArray<FlowData> NewFlowField;
    [WriteOnly] public NativeArray<IntegrationTile> NewIntegrationField;
    public void Execute(int index)
    {
        if(index < oldFieldLength)
        {
            NewFlowField[index] = OldFlowField[index];
            NewIntegrationField[index] = OldIntegrationField[index];
        }
        else
        {
            NewIntegrationField[index] = new IntegrationTile(float.MaxValue, IntegrationMark.None);
        }
        
    }
}