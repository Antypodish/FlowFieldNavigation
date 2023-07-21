using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor.Profiling.Memory.Experimental;

public struct PreallocationPack
{
    public NativeList<int> PortalSequence;
    public NativeList<int> PortalSequenceBorders;
    public NativeArray<PortalTraversalData> PortalTraversalDataArray;
    public NativeArray<DijkstraTile> TargetSectorCosts;
    public NativeQueue<LocalIndex1d> BlockedWaveFronts;
    public UnsafeList<int> SectorToPicked;
    public NativeList<int> PickedToSector;
    public NativeArray<int> FlowFieldLength;
}
