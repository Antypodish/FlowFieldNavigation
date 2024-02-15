using Unity.Mathematics;


namespace FlowFieldNavigation
{
    internal struct AgentMovementData
    {
        internal float3 Position;
        internal float2 Destination;
        internal float2 DesiredDirection;
        internal float2 CurrentDirection;
        internal float AlignmentMultiplierPercentage;
        internal float Speed;
        internal float Radius;
        internal float LandOffset;
        internal int FlockIndex;
        internal int PathId;
        internal int TensionPowerIndex;
        internal byte SplitInterval;
        internal byte SplitInfo;
        internal AgentStatus Status;
        internal AvoidanceStatus Avoidance;
        internal MovingAvoidanceStatus MovingAvoidance;
        internal AgentRoutineStatus RoutineStatus;
    }
    internal enum AgentRoutineStatus : byte
    {
        None = 0,
        Traversed = 1,
    }
    internal enum AvoidanceStatus : byte
    {
        None = 0,
        R = 1,
        L = 2,
    }
    internal enum MovingAvoidanceStatus : byte
    {
        None = 0,
        R = 1,
        L = 2,
    }

}