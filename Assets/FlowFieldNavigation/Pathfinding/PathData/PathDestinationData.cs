using Unity.Mathematics;

internal struct PathDestinationData
{
    internal DestinationType DestinationType;
    internal int TargetAgentIndex;
    internal int Offset;
    internal float2 Destination;
    internal float2 DesiredDestination;
}