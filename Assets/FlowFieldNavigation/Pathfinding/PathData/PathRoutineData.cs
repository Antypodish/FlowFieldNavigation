using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

internal struct PathRoutineData
{
    internal PathTask Task;
    internal DynamicDestinationState DestinationState;
    internal int FlowRequestSourceStart;
    internal int FlowRequestSourceCount;
    internal int PathAdditionSourceStart;
    internal int PathAdditionSourceCount;
    internal bool PathReconstructionFlag;
}
[Flags]
internal enum PathTask : byte
{
    FlowRequest = 1,
    PathAdditionRequest = 2,
    Reconstruct = 4,
};
internal enum DynamicDestinationState : byte
{
    None,
    Moved,
    OutOfReach,
};