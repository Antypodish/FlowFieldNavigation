using Unity.Mathematics;

public struct PathDestinationData
{
    public DestinationType DestinationType;
    public int TargetAgentIndex;
    public int Offset;
    public float2 Destination;
    public float2 DesiredDestination;
}