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
    public Vector2 Destination;
    public NativeArray<Vector3> Sources;
    public NativeArray<float> PortalDistances;
    public NativeArray<int> ConnectionIndicies;
    public NativeArray<PortalMark> PortalMarks;
    public NativeList<PortalSequence> PortalSequence;
    public NativeArray<int> SectorMarks;
    public NativeList<IntegrationFieldSector> IntegrationField;
    public NativeList<FlowFieldSector> FlowField;
    public NativeQueue<LocalIndex1d> BlockedWaveFronts;
    public NativeQueue<LocalIndex1d> intqueue;

    public void Dispose()
    {
        intqueue.Dispose();
        BlockedWaveFronts.Dispose();
        Sources.Dispose();
        PortalDistances.Dispose();
        ConnectionIndicies.Dispose();
        PortalMarks.Dispose();
        PortalSequence.Dispose();
        SectorMarks.Dispose();
        for(int i = 1; i < IntegrationField.Length; i++)
        {
            IntegrationField[i].integrationSector.Dispose();
        }
        for (int i = 1; i < FlowField.Length; i++)
        {
            FlowField[i].flowfieldSector.Dispose();
        }
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
        return FlowField[SectorMarks[sector1d]].flowfieldSector[local1d];
    }
}
public enum PathState : byte
{
    Clean = 0,
    ToBeUpdated = 1,
    ToBeDisposed = 2,
}