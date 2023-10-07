using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

public struct AgentMovementData
{
    public float3 Position;
    public float2 Destination;
    public float2 DesiredDirection;
    public float2 CurrentDirection;
    public float2 NextDirection;
    public float2 SeperationForce;
    public Waypoint Waypoint;
    public float Speed;
    public int Offset;
    public float Radius;
    public ushort Local1d;
    public AgentStatus Status;
    public AvoidanceStatus Avoidance;
    public AgentRoutineStatus RoutineStatus;
    public UnsafeList<FlowData> FlowField;
    public UnsafeList<int> SectorToPicked;
    public int PathId;
    public int TensionPowerIndex;

    public byte SplitInterval;
    public byte SplitInfo;
}
public enum AgentRoutineStatus : byte
{
    None = 0,
    Traversed = 1,
}
public enum AvoidanceStatus : byte
{
    None = 0,
    R = 1,
    L = 2,
}