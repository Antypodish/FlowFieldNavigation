using Unity.Collections.LowLevel.Unsafe;

internal struct PathFlowData
{
    internal UnsafeList<FlowData> FlowField;
    internal UnsafeLOSBitmap LOSMap;
    internal UnsafeList<FlowData> DynamicAreaFlowField;

    internal void Dispose()
    {
        LOSMap.Dispose();
        DynamicAreaFlowField.Dispose();
    }
}
