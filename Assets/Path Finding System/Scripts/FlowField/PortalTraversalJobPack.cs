using Unity.Collections;
using Unity.Jobs;

public struct PortalTraversalJobPack
{
    public NewPortalNodeTraversalJob PortalTravJob;
    public int PathIndex;

    public PathHandle Schedule()
    {
        return new PathHandle()
        {
            Handle = PortalTravJob.Schedule(),
            PathIndex = PathIndex
        };
    }
}