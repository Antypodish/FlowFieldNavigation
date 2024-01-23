using Unity.Jobs;

internal struct PathPipelineInfoWithHandle
{
    internal JobHandle Handle;
    internal int PathIndex;
    internal DynamicDestinationState DestinationState;
    internal PathPipelineInfoWithHandle(JobHandle handle, int pathIndex, DynamicDestinationState destinationState = DynamicDestinationState.None)
    {
        Handle = handle;
        PathIndex = pathIndex;
        DestinationState = destinationState;
    }
}