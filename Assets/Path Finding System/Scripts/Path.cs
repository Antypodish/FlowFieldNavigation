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
    public NativeArray<PortalTraversalData> PortalTraversalDataArray;
    public NativeList<ActivePortal> PortalSequence;
    public UnsafeList<int> SectorToPicked;
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
    public void Dispose()
    {
        PathAdditionSequenceBorderStartIndex.Dispose();
        NotActivePortalList.Dispose();
        NewPickedSectorStartIndex.Dispose();
        SectorFlowStartIndiciesToCalculateFlow.Dispose();
        SectorFlowStartIndiciesToCalculateIntegration.Dispose();
        LOSMap.Dispose();
    }
    public void SetState(PathState state)
    {
        State = state;
    }
    public FlowData GetFlow(int local1d, int sector1d)
    {
        return FlowField[SectorToPicked[sector1d] + local1d];
    }
}
public enum PathState : byte
{
    Clean = 0,
    ToBeUpdated = 1,
    ToBeDisposed = 2,
    Removed,
}