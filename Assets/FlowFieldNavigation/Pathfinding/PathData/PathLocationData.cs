using Unity.Collections.LowLevel.Unsafe;

internal struct PathLocationData
{
    internal UnsafeList<int> SectorToPicked;
    internal UnsafeList<SectorFlowStart> DynamicAreaPickedSectorFlowStarts;
}