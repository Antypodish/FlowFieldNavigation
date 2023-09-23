using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

public struct AgentMovementData
{
    public float3 Position;
    public float2 Destination;
    public float2 Flow;
    public float2 Velocity;
    public Waypoint Waypoint;
    public float Speed;
    public int Offset;
    public float Radius;
    public ushort Local1d;
    public AgentStatus Status;
    public AgentRoutineStatus RoutineStatus;
    public UnsafeList<FlowData> FlowField;
    public UnsafeList<int> SectorToPicked;
    public int PathId;
    public int ObstacleIndex;
}
public enum AgentRoutineStatus : byte
{
    None = 0,
    Traversed = 1,
}