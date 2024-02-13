using System;
using Unity.Mathematics;

public struct AgentData
{
    public float Speed;
    public AgentStatus Status;
    internal AvoidanceStatus Avoidance;
    internal MovingAvoidanceStatus MovingAvoidance;
    public float2 Destination;
    public float2 Direction;
    public float2 DesiredDirection;
    public float3 DirectionWithHeigth;
    public float2 Seperation;
    public float3 Position;
    public float LandOffset;
    public float Radius;

    public byte SplitInterval;
    public byte SplitInfo;

    public int StopDistanceIndex;

    public void SetStatusBit(AgentStatus status)
    {
        Status |= status;
    }
    public void ClearStatusBit(AgentStatus status)
    {
        Status = ~(~Status | status);
    }
}

[Flags]
public enum AgentStatus : byte
{
    Moving = 1,
    HoldGround = 2,
}