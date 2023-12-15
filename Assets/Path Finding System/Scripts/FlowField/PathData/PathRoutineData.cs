using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

public struct PathRoutineData
{
    public PathTask Task;
    public DynamicDestinationState DestinationState;
    public int FlowRequestSourceStart;
    public int FlowRequestSourceCount;
    public int PathAdditionSourceStart;
    public int PathAdditionSourceCount;
    public int ReconstructionRequestIndex;
}
[Flags]
public enum PathTask : byte
{
    FlowRequest = 1,
    PathAdditionRequest = 2,
};
public enum DynamicDestinationState : byte
{
    None,
    Moved,
    OutOfReach,
};