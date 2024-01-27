using Unity.Mathematics;

internal struct PathRequest
{
    internal float2 Destination;
    internal int TargetAgentIndex;
    internal int DerivedRequestStartIndex;
    internal int DerivedRequestCount;
    internal DestinationType Type;
    internal ushort OffsetMask;
    internal int FlockIndex;
    internal bool ReconstructionFlag;
    internal PathRequest(float2 destination)
    {
        Destination = destination;
        Type = DestinationType.StaticDestination;
        TargetAgentIndex = 0;
        DerivedRequestCount = 0;
        DerivedRequestStartIndex = 0;
        OffsetMask = 0;
        FlockIndex = 0;
        ReconstructionFlag = false;
    }

    internal PathRequest(int targetAgentIndex)
    {
        TargetAgentIndex = targetAgentIndex;
        Type = DestinationType.DynamicDestination;
        Destination = 0;
        DerivedRequestCount = 0;
        DerivedRequestStartIndex = 0;
        OffsetMask = 0;
        FlockIndex = 0;
        ReconstructionFlag = false;
    }
    internal PathRequest(PathRequestRecord requestRecord)
    {
        Destination = requestRecord.Destination;
        Type = requestRecord.Type;
        TargetAgentIndex = requestRecord.TargetAgent;
        DerivedRequestCount = 0;
        DerivedRequestStartIndex = 0;
        OffsetMask = 0;
        FlockIndex = 0;
        ReconstructionFlag = false;
    }
}

internal struct OffsetDerivedPathRequest
{
    internal float2 Destination;
    internal int TargetAgentIndex;
    internal int DerivedFialRequestStartIndex;
    internal int DerivedFinalRequestCount;
    internal int Offset;
    internal DestinationType Type;
    internal int FlockIndex;
    internal bool ReconstructionFlag;

    internal OffsetDerivedPathRequest(PathRequest initialPathRequest, int offset)
    {
        Destination = initialPathRequest.Destination;
        TargetAgentIndex = initialPathRequest.TargetAgentIndex;
        Offset = offset;
        Type = initialPathRequest.Type;
        DerivedFialRequestStartIndex = 0;
        DerivedFinalRequestCount = 0;
        FlockIndex = initialPathRequest.FlockIndex;
        ReconstructionFlag = initialPathRequest.ReconstructionFlag;
    }

    internal bool IsCreated()
    {
        return Type != DestinationType.None;
    }
}

internal struct FinalPathRequest
{
    internal float2 Destination;
    internal float2 DesiredDestination;
    internal int TargetAgentIndex;
    internal int SourcePositionStartIndex;
    internal int SourceCount;
    internal int Offset;
    internal int PathIndex;
    internal int SourceIsland;
    internal DestinationType Type;
    internal int FlockIndex;
    internal bool ReconstructionFlag;

    internal FinalPathRequest(OffsetDerivedPathRequest derivedReq, int sourceIsland)
    {
        Destination = derivedReq.Destination;
        DesiredDestination = derivedReq.Destination;
        Type = derivedReq.Type;
        Offset = derivedReq.Offset;
        TargetAgentIndex = derivedReq.TargetAgentIndex;
        SourceIsland = sourceIsland;
        FlockIndex = derivedReq.FlockIndex;
        ReconstructionFlag = derivedReq.ReconstructionFlag;

        SourceCount = 0;
        SourcePositionStartIndex = 0;
        PathIndex = 0;
    }
    internal bool IsValid() => SourceCount != 0;
}