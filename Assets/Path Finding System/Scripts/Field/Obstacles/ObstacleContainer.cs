using Unity.Collections;

public class ObstacleContainer
{
    public NativeList<Obstacle> ObstacleList;

    public ObstacleContainer()
    {
        ObstacleList = new NativeList<Obstacle>(Allocator.Persistent);
    }
}
