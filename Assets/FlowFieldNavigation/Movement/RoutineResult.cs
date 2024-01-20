using Unity.Mathematics;

public struct RoutineResult
{
    public float2 NewDirection;
    public float3 NewSeperation;
    public float HeightChange;
    public byte NewSplitInterval;
    public byte NewSplitInfo;
    public AvoidanceStatus NewAvoidance;
    public MovingAvoidanceStatus NewMovingAvoidance;
    public bool HasForeignInFront;
}
