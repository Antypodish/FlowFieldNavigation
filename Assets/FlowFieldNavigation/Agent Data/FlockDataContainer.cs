using Unity.Collections;

public class FlockDataContainer
{
    internal NativeList<Flock> FlockList;
    public NativeList<int> UnusedFlockIndexList;

    public FlockDataContainer()
    {
        FlockList = new NativeList<Flock>(Allocator.Persistent);
        FlockList.Add(new Flock());
        UnusedFlockIndexList = new NativeList<int>(Allocator.Persistent);
    }
    internal void DisposeAll()
    {
        FlockList.Dispose();
        UnusedFlockIndexList.Dispose();
    }
}
