using Unity.Mathematics;

public struct RoutineResult
{
    public float2 NewDirection;
    public float2 NewSeperation;
    public byte NewSplitInterval;
    public byte NewSplitInfo;
    public AvoidanceStatus NewAvoidance;
    public MovingAvoidanceStatus NewMovingAvoidance;
}
