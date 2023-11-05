using Unity.Mathematics;

public struct PathData
{
    public int Index;
    public int AgentCount;
    public float2 Target;
    public PathTask Task;
    public PathState State;
}
public enum PathTask : byte
{
    Reconstruct,
    UpdateTargetSector,
}