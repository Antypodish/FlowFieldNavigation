using Unity.Collections;

internal class FlockDataContainer
{
    internal NativeList<Flock> FlockList;
    internal NativeList<int> UnusedFlockIndexList;

    internal FlockDataContainer()
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
