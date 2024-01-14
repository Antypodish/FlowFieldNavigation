using Unity.Collections;
using Unity.Mathematics;

public class ObstacleContainer
{
    public NativeList<Obstacle> ObstacleList;
    public NativeList<int> RemovedIndexList;

    public ObstacleContainer()
    {
        ObstacleList = new NativeList<Obstacle>(Allocator.Persistent);
        RemovedIndexList = new NativeList<int>(Allocator.Persistent);
    }
}

public struct ObstacleRequest
{
    public float2 Position;
    public float2 HalfSize;

    public ObstacleRequest(float2 pos, float2 halfSize) { Position = pos; HalfSize = halfSize; }
}
public struct CostEdit
{
    public int2 BotLeftBound;
    public int2 TopRightBound;
    public CostEditType EditType;
}
public enum CostEditType : byte
{
    Set,
    Clear,
}
public enum ObstacleState : byte
{
    Live,
    Removed,
}
public struct Obstacle
{
    public int2 BotLeftBound;
    public int2 TopRightBound;
    public ObstacleState State;
}