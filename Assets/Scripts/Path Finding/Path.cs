using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

public class Path
{
    public int RoutineMark = -1;
    public int Subscriber = 0;
    public int Offset;
    public PathState State;
    public bool IsCalculated = false;
    public int2 TargetIndex;
    public Vector2 Destination;
    public NativeArray<Vector3> Sources;
    public NativeArray<PortalTraversalData> PortalTraversalDataArray;
    public NativeList<int> PortalSequence;
    public NativeArray<int> SectorToPicked;
    public NativeList<int> PickedToSector;
    public NativeArray<IntegrationTile> IntegrationField;
    public NativeArray<FlowData> FlowField;
    public NativeQueue<LocalIndex1d> BlockedWaveFronts;
    public NativeArray<DijkstraTile> TargetSectorCosts;
    public NativeList<int> PortalSequenceBorders;

    public void DisposeTemp()
    {
        BlockedWaveFronts.Dispose();
    }
    public void Dispose()
    {
        PickedToSector.Dispose();
        PortalSequenceBorders.Dispose();
        Sources.Dispose();
        TargetSectorCosts.Dispose();
        PortalTraversalDataArray.Dispose();
        PortalSequence.Dispose();
        SectorToPicked.Dispose();
        IntegrationField.Dispose();
        FlowField.Dispose();
    }
    public void Subscribe()
    {
        Subscriber++;
    }
    public void Unsubscribe()
    {
        Subscriber--;
        if (Subscriber == 0)
        {
            State = PathState.ToBeDisposed;
        }
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
}