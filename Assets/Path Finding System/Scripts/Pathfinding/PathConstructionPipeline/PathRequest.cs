using Unity.Mathematics;

public struct PathRequest
{
    public float2 Destination;
    public int TargetAgentIndex;
    public int DerivedRequestStartIndex;
    public int DerivedRequestCount;
    public DestinationType Type;
    public ushort OffsetMask;

    public PathRequest(float2 destination)
    {
        Destination = destination;
        Type = DestinationType.StaticDestination;
        TargetAgentIndex = 0;
        DerivedRequestCount = 0;
        DerivedRequestStartIndex = 0;
        OffsetMask = 0;
    }

    public PathRequest(int targetAgentIndex)
    {
        TargetAgentIndex = targetAgentIndex;
        Type = DestinationType.DynamicDestination;
        Destination = 0;
        DerivedRequestCount = 0;
        DerivedRequestStartIndex = 0;
        OffsetMask = 0;
    }
}

public struct OffsetDerivedPathRequest
{
    public float2 Destination;
    public int TargetAgentIndex;
    public int DerivedFialRequestStartIndex;
    public int DerivedFinalRequestCount;
    public int Offset;
    public DestinationType Type;

    public OffsetDerivedPathRequest(PathRequest initialPathRequest, int offset)
    {
        Destination = initialPathRequest.Destination;
        TargetAgentIndex = initialPathRequest.TargetAgentIndex;
        Offset = offset;
        Type = initialPathRequest.Type;
        DerivedFialRequestStartIndex = 0;
        DerivedFinalRequestCount = 0;
    }

    public bool IsCreated()
    {
        return Type != DestinationType.None;
    }
}

public struct FinalPathRequest
{
    public float2 Destination;
    public float2 DesiredDestination;
    public int TargetAgentIndex;
    public int SourcePositionStartIndex;
    public int SourceCount;
    public int Offset;
    public int PathIndex;
    public int SourceIsland;
    public DestinationType Type;

    public FinalPathRequest(OffsetDerivedPathRequest derivedReq, int sourceIsland)
    {
        Destination = derivedReq.Destination;
        DesiredDestination = derivedReq.Destination;
        Type = derivedReq.Type;
        Offset = derivedReq.Offset;
        TargetAgentIndex = derivedReq.TargetAgentIndex;
        SourceIsland = sourceIsland;

        SourceCount = 0;
        SourcePositionStartIndex = 0;
        PathIndex = 0;
    }
    public bool IsValid() => SourceCount != 0;
}
public struct PostponedPathRequests
{
    public float2 Destination;
    public int TargetAgentIndex;
    public DestinationType Type;

    public PostponedPathRequests(FinalPathRequest finalRequest)
    {
        Destination = finalRequest.Destination;
        Type = finalRequest.Type;
        TargetAgentIndex = finalRequest.TargetAgentIndex;
    }
}