using Unity.Collections;
using Unity.Mathematics;

internal class ObstacleContainer
{
    internal NativeList<Obstacle> ObstacleList;
    internal NativeList<int> RemovedIndexList;

    internal ObstacleContainer()
    {
        ObstacleList = new NativeList<Obstacle>(Allocator.Persistent);
        RemovedIndexList = new NativeList<int>(Allocator.Persistent);
    }
    internal void DisposeAll()
    {
        ObstacleList.Dispose();
        RemovedIndexList.Dispose();
    }
}
internal struct CostEdit
{
    internal int2 BotLeftBound;
    internal int2 TopRightBound;
    internal CostEditType EditType;
}
internal enum CostEditType : byte
{
    Set,
    Clear,
}
internal enum ObstacleState : byte
{
    Live,
    Removed,
}
internal struct Obstacle
{
    internal int2 BotLeftBound;
    internal int2 TopRightBound;
    internal ObstacleState State;
}