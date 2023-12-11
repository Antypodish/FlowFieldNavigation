using Unity.Collections.LowLevel.Unsafe;

public struct PathLocationData
{
    public UnsafeList<int> SectorToPicked;
    public UnsafeList<SectorFlowStart> DynamicAreaPickedSectorFlowStarts;

    public void Dispose()
    {
        DynamicAreaPickedSectorFlowStarts.Dispose();
    }
}