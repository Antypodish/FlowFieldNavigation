using Unity.Jobs;

namespace FlowFieldNavigation
{
    internal struct PathfindingPipelineStateHandle
    {
        internal JobHandle Handle;
        internal PathfindingPipelineState State;
    }

    internal enum PathfindingPipelineState : byte
    {
        None = 0,
        Intermediate = 1,
        Final = 2,
    }
}