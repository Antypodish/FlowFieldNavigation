using Unity.Jobs;

public struct PortalTraversalHandle
{
    public JobHandle Handle;
    public Path path;

    public void Complete()
    {
        Handle.Complete();
    }
}