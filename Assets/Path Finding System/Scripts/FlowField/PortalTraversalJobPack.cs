using Unity.Collections;
using Unity.Jobs;

public struct PortalTraversalJobPack
{
    public NewPortalNodeTraversalJob PortalTravJob;
    public Path Path;

    public PathHandle Schedule(JobHandle dependancy)
    {
        return new PathHandle()
        {
            Handle = PortalTravJob.Schedule(dependancy),
            Path = Path,
        };
    }
}