using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;
public struct PathData
{
    public float2 Target;
    public PathTask Task;
    public PathState State;
    public PathType Type;
    public UnsafeList<FlowData> FlowField;
    public UnsafeList<PathSectorState> SectorStateTable;
    public UnsafeList<int> SectorToPicked;
    public int TargetAgentIndex;
    public int FlowRequestSourceStart;
    public int FlowRequestSourceCount;
    public int PathAdditionSourceStart;
    public int PathAdditionSourceCount;
    public int ReconstructionRequestIndex;
}
public enum PathTask : byte
{
    FlowRequest = 1,
    PathAdditionRequest = 2,
}