using Unity.Collections.LowLevel.Unsafe;


namespace FlowFieldNavigation
{
    internal struct PathFlowData
    {
        internal UnsafeList<FlowData> FlowField;
        internal UnsafeLOSBitmap LOSMap;
        internal UnsafeList<FlowData> DynamicAreaFlowField;

        internal void Dispose()
        {
            LOSMap.Dispose();
        }
    }


}