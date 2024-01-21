using Unity.Mathematics;

internal struct RoutineResult
{
    internal float2 NewDirection;
    internal float3 NewSeperation;
    internal float HeightChange;
    internal byte NewSplitInterval;
    internal byte NewSplitInfo;
    internal AvoidanceStatus NewAvoidance;
    internal MovingAvoidanceStatus NewMovingAvoidance;
    internal bool HasForeignInFront;
}
