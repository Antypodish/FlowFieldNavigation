using Unity.Collections.LowLevel.Unsafe;


namespace FlowFieldNavigation
{
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


}