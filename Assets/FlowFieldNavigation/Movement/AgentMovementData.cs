using Unity.Mathematics;

public struct AgentMovementData
{
    public float3 Position;
    public float2 Destination;
    public float2 DesiredDirection;
    public float2 CurrentDirection;
    public float AlignmentMultiplierPercentage;
    public float Speed;
    public float Radius;
    public int FlockIndex;
    public int PathId;
    public int TensionPowerIndex;
    public byte SplitInterval;
    public byte SplitInfo;
    public AgentStatus Status;
    public AvoidanceStatus Avoidance;
    public MovingAvoidanceStatus MovingAvoidance;
    public AgentRoutineStatus RoutineStatus;
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
public enum MovingAvoidanceStatus: byte
{
    None = 0,
    R = 1,
    L = 2,
}