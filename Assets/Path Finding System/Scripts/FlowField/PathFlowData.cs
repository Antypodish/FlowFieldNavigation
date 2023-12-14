using Unity.Collections.LowLevel.Unsafe;

public struct PathFlowData
{
    public UnsafeList<FlowData> FlowField;
    public UnsafeLOSBitmap LOSMap;
    public UnsafeList<FlowData> DynamicAreaFlowField;

    public void Dispose()
    {
        LOSMap.Dispose();
        DynamicAreaFlowField.Dispose();
    }
}
