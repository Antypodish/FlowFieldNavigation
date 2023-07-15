using Unity.Jobs;

public struct PortalAdditionTraversalHandle
{
    public JobHandle Handle;
    public Path Path;

    public PortalAdditionTraversalHandle(JobHandle handle, Path path) { Handle = handle; Path = path; }
}