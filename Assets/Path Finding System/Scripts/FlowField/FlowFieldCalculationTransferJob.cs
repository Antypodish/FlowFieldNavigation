using Unity.Jobs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;

[BurstCompile]
public struct FlowFieldCalculationTransferJob : IJob
{
    [WriteOnly] public UnsafeList<FlowData> FlowField;
    public FlowFieldCalculationBufferParent CalculationBufferParent;
    public void Execute()
    {
        UnsafeList<FlowFieldCalculationBuffer> calculationBuffers = CalculationBufferParent.BufferParent;
        for(int i = 0; i < calculationBuffers.Length; i++)
        {
            FlowFieldCalculationBuffer calculationBuffer = calculationBuffers[i];
            int flowStartIndex = calculationBuffer.FlowFieldStartIndex;
            UnsafeList<FlowData> buffer = calculationBuffer.Buffer;
            for(int j = 0; j < buffer.Length; j++)
            {
                FlowField[flowStartIndex + j] = buffer[j];
            }
        }
    }
}
