using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

public struct PreallocationPack
{
    public NativeList<ActivePortal> PortalSequence;
    public NativeList<int> PortalSequenceBorders;
    public NativeArray<PortalTraversalData> PortalTraversalDataArray;
    public NativeArray<DijkstraTile> TargetSectorCosts;
    public UnsafeList<int> SectorToPicked;
    public NativeList<int> PickedToSector;
    public NativeArray<int> FlowFieldLength;
}
