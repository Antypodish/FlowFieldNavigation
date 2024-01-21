using Unity.Jobs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;

[BurstCompile]
internal struct FlowFieldCalculationTransferJob : IJob
{
    [WriteOnly] internal UnsafeList<FlowData> FlowField;
    internal FlowFieldCalculationBufferParent CalculationBufferParent;
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
