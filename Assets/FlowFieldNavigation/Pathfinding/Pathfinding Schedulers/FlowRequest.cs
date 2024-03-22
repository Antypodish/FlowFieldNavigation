namespace FlowFieldNavigation
{
    internal struct FlowRequest
    {
        internal int PathIndex;
        internal Slice SourceSlice;

        internal FlowRequest(int pathIndex, Slice sourceSlice)
        {
            PathIndex = pathIndex;
            SourceSlice = sourceSlice;
        }
    }
}