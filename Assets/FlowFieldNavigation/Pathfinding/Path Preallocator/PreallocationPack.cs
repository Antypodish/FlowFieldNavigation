using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

internal struct PreallocationPack
{
    internal NativeList<ActivePortal> PortalSequence;
    internal NativeList<int> PortalSequenceBorders;
    internal NativeArray<PortalTraversalData> PortalTraversalDataArray;
    internal UnsafeList<DijkstraTile> TargetSectorCosts;
    internal UnsafeList<int> SectorToPicked;
    internal NativeList<int> PickedToSector;
    internal NativeArray<int> FlowFieldLength;
    internal NativeQueue<int> PortalTraversalFastMarchingQueue;
    internal NativeList<int> AStartTraverseIndexList;
    internal NativeList<int> TargetSectorPortalIndexList;
    internal NativeList<int> SourcePortalIndexList;
    internal UnsafeList<PathSectorState> SectorStateTable;
}
