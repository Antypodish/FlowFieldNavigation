using Unity.Collections;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Jobs;

[BurstCompile]
public struct ObstacleRequestToObstacleJob : IJobParallelFor
{
    public float TileSize;
    public int FieldColAmount;
    public int FieldRowAmount;
    [ReadOnly] public NativeArray<ObstacleRequest> ObstacleRequests;
    [WriteOnly] public NativeArray<Obstacle> NewObstacles;
    public void Execute(int index)
    {
        ObstacleRequest obstacleRequest = ObstacleRequests[index];
        float2 botLeft = obstacleRequest.Position - obstacleRequest.HalfSize;
        float2 topRight = obstacleRequest.Position + obstacleRequest.HalfSize;
        int2 botLeftBound = FlowFieldUtilities.PosTo2D(botLeft, TileSize);
        int2 toprightBound = FlowFieldUtilities.PosTo2D(topRight, TileSize);

        botLeftBound.x = math.select(botLeftBound.x, 0, botLeftBound.x < 0);
        botLeftBound.y = math.select(botLeftBound.y, 0, botLeftBound.y < 0);
        toprightBound.x = math.select(toprightBound.x, FieldColAmount - 1, toprightBound.x >= FieldColAmount);
        toprightBound.y = math.select(toprightBound.y, FieldRowAmount - 1, toprightBound.y >= FieldRowAmount);
        NewObstacles[index] = new Obstacle()
        {
            BotLeftBound = botLeftBound,
            TopRightBound = toprightBound,
        };
    }
}
