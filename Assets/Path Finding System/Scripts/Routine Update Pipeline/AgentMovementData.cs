using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

public struct AgentMovementData
{
    public float3 Position;
    public float2 Destination;
    public float2 Flow;
    public Waypoint Waypoint;
    public float Speed;
    public int Offset;
    public float Radius;
    public ushort Local1d;
    public ushort Sector1d;
    public bool OutOfFieldFlag;
    public AgentStatus Status;
    public UnsafeList<FlowData> FlowField;
    public UnsafeList<int> SectorToPicked;
    public int PathId;
}