using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEditor.Profiling.Memory.Experimental;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

public struct Path
{
    public PathState State;
    public int Offset;
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

    public void Dispose()
    {
        PortalDistances.Dispose();
        ConnectionIndicies.Dispose();
        PortalMarks.Dispose();
        PortalSequence.Dispose();
        PickedSectors.Dispose();
        SectorMarks.Dispose();
        IntegrationField.Dispose();
        FlowField.Dispose();
        Sources.Dispose();
    }
}
public enum PathState : byte
{
    Clean = 0,
    Dirty = 1
}