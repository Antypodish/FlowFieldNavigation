using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

public class Path
{
    public int Id;
    public int Offset;
    public PathState State;
    public PathType Type;
    public int TargetAgentIndex;
    public bool IsCalculated = false;
    public int2 TargetIndex;
    public Vector2 Destination;
    public DynamicArea DynamicArea;
    public NativeArray<PortalTraversalData> PortalTraversalDataArray;
    public NativeList<ActivePortal> PortalSequence;
    public NativeList<int> PickedToSector;
    public NativeList<IntegrationTile> IntegrationField;
    public UnsafeList<FlowData> FlowField;
    public NativeArray<DijkstraTile> TargetSectorCosts;
    public NativeList<int> PortalSequenceBorders;
    public NativeArray<int> FlowFieldLength;
    public NativeQueue<int> PortalTraversalFastMarchingQueue;
    public NativeList<int> AStartTraverseIndexList;
    public NativeList<int> TargetSectorPortalIndexList;
    public NativeList<int> SourcePortalIndexList;
    public NativeList<UnsafeList<ActiveWaveFront>> ActiveWaveFrontList;
    public UnsafeList<PathSectorState> SectorStateTable;
    public NativeArray<int> PathAdditionSequenceBorderStartIndex;
    public NativeList<int> NotActivePortalList;
    public NativeArray<int> NewPickedSectorStartIndex;
    public NativeList<int> SectorFlowStartIndiciesToCalculateIntegration;
    public NativeList<int> SectorFlowStartIndiciesToCalculateFlow;
    public UnsafeLOSBitmap LOSMap;
    public NativeArray<SectorsWihinLOSArgument> SectorWithinLOSState;
    public void Dispose()
    {
        PathAdditionSequenceBorderStartIndex.Dispose();
        NotActivePortalList.Dispose();
        NewPickedSectorStartIndex.Dispose();
        SectorFlowStartIndiciesToCalculateFlow.Dispose();
        SectorFlowStartIndiciesToCalculateIntegration.Dispose();
        LOSMap.Dispose();
        SectorWithinLOSState.Dispose();
        DynamicArea.Dispose();
    }
    public bool LOSCalculated(UnsafeList<int> sectorToPicked)
    {
        int sectorColAmount = FlowFieldUtilities.SectorColAmount;
        int sectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount;
        LocalIndex1d local = FlowFieldUtilities.GetLocal1D(TargetIndex, sectorColAmount, sectorMatrixColAmount);
        return (IntegrationField[sectorToPicked[local.sector] + local.index].Mark & IntegrationMark.LOSPass) == IntegrationMark.LOSPass;
    }
}
public enum PathState : byte
{
    Clean = 0,
    ToBeUpdated = 1,
    ToBeDisposed = 2,
    Removed,
}
public enum SectorsWihinLOSArgument : byte
{
    None = 0,
    RequestedSectorWithinLOS = 1,
    AddedSectorWithinLOS = 2,
};