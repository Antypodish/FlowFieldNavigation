﻿using Unity.Collections;
using UnityEngine;

public class Path
{
    public int Offset;
    public PathState State;
    public bool IsCalculated = false;
    public Vector3 Destination;
    public NativeArray<Vector3> Sources;
    public NativeArray<float> PortalDistances;
    public NativeArray<int> ConnectionIndicies;
    public NativeArray<PortalMark> PortalMarks;
    public NativeList<PortalSequence> PortalSequence;
    public NativeList<int> PickedSectors;
    public NativeArray<bool> SectorMarks;
    public NativeArray<IntegrationTile> IntegrationField;
    public NativeArray<FlowData> FlowField;
    public NativeList<int> CornerTiles;
    public NativeList<Vector2> CornerPos;

    public void Dispose()
    {
        CornerPos.Dispose();
        CornerTiles.Dispose();
        Sources.Dispose();
        PortalDistances.Dispose();
        ConnectionIndicies.Dispose();
        PortalMarks.Dispose();
        PortalSequence.Dispose();
        PickedSectors.Dispose();
        SectorMarks.Dispose();
        IntegrationField.Dispose();
        FlowField.Dispose();
    }
    public void SetState(PathState state)
    {
        State = state;
    }
}
public enum PathState : byte
{
    Clean = 0,
    ToBeUpdated = 1,
    ToBeDisposed = 2,
}