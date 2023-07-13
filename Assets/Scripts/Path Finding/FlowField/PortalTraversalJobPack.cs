using Unity.Collections;
using Unity.Jobs;

public struct PortalTraversalJobPack
{
    public UnsafeListDefaultSetterJob<int> ClearJob;
    public PortalNodeTraversalJob PortalTravJob;
    public Path Path;

    public PortalTraversalHandle Schedule(JobHandle dependancy)
    {
        return new PortalTraversalHandle()
        {
            Handle = PortalTravJob.Schedule(ClearJob.Schedule(dependancy)),
            path = Path,
        };
    }
    public PortalTraversalHandle Schedule()
    {
        return new PortalTraversalHandle()
        {
            Handle = PortalTravJob.Schedule(ClearJob.Schedule()),
            path = Path,
        };
    }
}