namespace FlowFieldNavigation
{
    internal struct LosRequest
    {
        internal int PathIndex;
        internal DynamicDestinationState DestinationState;

        internal LosRequest(int pathIndex, DynamicDestinationState destinationState)
        {
            PathIndex = pathIndex;
            DestinationState = destinationState;
        }
    }
}