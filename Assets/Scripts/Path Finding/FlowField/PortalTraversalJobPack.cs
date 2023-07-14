using Unity.Collections;
using Unity.Jobs;

public struct PortalTraversalJobPack
{
    public PortalNodeTraversalJob PortalTravJob;
    public Path Path;

    public PortalTraversalHandle Schedule(JobHandle dependancy)
    {
        return new PortalTraversalHandle()
        {
            Handle = PortalTravJob.Schedule(dependancy),
            path = Path,
        };
    }
    public PortalTraversalHandle Schedule()
    {
        return new PortalTraversalHandle()
        {
            Handle = PortalTravJob.Schedule(),
            path = Path,
        };
    }
}