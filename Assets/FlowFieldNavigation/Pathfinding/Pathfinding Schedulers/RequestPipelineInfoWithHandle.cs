using Unity.Jobs;


namespace FlowFieldNavigation
{
    internal struct RequestPipelineInfoWithHandle
    {
        internal JobHandle Handle;
        internal int PathIndex;
        internal int RequestIndex;
        internal DynamicDestinationState DestinationState;

        internal RequestPipelineInfoWithHandle(JobHandle handle, int pathIndex, int requestIndex, DynamicDestinationState destinationState = DynamicDestinationState.None)
        {
            Handle = handle;
            PathIndex = pathIndex;
            RequestIndex = requestIndex;
            DestinationState = destinationState;
        }
        internal PathPipelineInfoWithHandle ToPathPipelineInfoWithHandle()
        {
            return new PathPipelineInfoWithHandle()
            {
                Handle = Handle,
                PathIndex = PathIndex,
                DestinationState = DestinationState,
            };
        }
    }


}