using Unity.Collections.LowLevel.Unsafe;

internal struct FlowFieldCalculationBuffer
{
    internal int FlowFieldStartIndex;
    internal UnsafeList<FlowData> Buffer;
}
internal struct FlowFieldCalculationBufferParent
{
    internal int PathIndex;
    internal UnsafeList<FlowFieldCalculationBuffer> BufferParent;
}
