using Unity.Jobs;

namespace FlowFieldNavigation
{
    internal struct PathPipelineReq
    {
        internal int PathIndex;
        internal Slice FlowReqSourceSlice;
        internal DynamicDestinationState DynamicDestinationState;
        internal JobHandle Handle;
    }
}