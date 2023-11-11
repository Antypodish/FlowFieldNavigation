using Unity.Collections.LowLevel.Unsafe;

public struct FlowFieldCalculationBuffer
{
    public int FlowFieldStartIndex;
    public UnsafeList<FlowData> Buffer;
}
public struct FlowFieldCalculationBufferParent
{
    public int PathIndex;
    public UnsafeList<FlowFieldCalculationBuffer> BufferParent;
}
