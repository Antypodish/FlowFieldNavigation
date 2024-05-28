using Unity.Mathematics;

namespace FlowFieldNavigation
{
    internal struct PathRequestRecord
    {
        internal float2 Destination;
        internal int TargetAgent;
        internal DestinationType Type;
        internal float Range;

        public PathRequestRecord(PathRequest pathRequest)
        {
            Destination = pathRequest.Destination;
            Type = pathRequest.Type;
            TargetAgent = pathRequest.TargetAgentIndex;
            Range = pathRequest.Range;
        }
    }

}