using Unity.Collections.LowLevel.Unsafe;

public struct FlowFieldSector
{
    public UnsafeList<FlowData> flowfieldSector;
    public int sectorIndex1d;

    public FlowFieldSector(int index)
    {
        sectorIndex1d = index;
        flowfieldSector = new UnsafeList<FlowData>();
    }
}
