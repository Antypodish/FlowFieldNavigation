using Unity.Mathematics;

namespace FlowFieldNavigation
{
    internal struct PathRequestRecord
    {
        internal float2 Destination;
        internal int TargetAgent;
        internal DestinationType Type;

        public PathRequestRecord(PathRequest pathRequest)
        {
            Destination = pathRequest.Destination;
            Type = pathRequest.Type;
            TargetAgent = pathRequest.TargetAgentIndex;
        }
    }

}