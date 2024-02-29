using Unity.Mathematics;


namespace FlowFieldNavigation
{
    internal struct Obstacle
    {
        internal int2 BotLeftBound;
        internal int2 TopRightBound;
        internal ObstacleState State;
    }
    internal enum ObstacleState : byte
    {
        Live,
        Removed,
    }

}