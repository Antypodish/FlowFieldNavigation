using Unity.Collections;
using Unity.Jobs;

public struct PortalTraversalJobPack
{
    public PortalNodeTraversalJob PortalTravJob;
    public int PathIndex;

    public NewPathHandle Schedule()
    {
        return new NewPathHandle()
        {
            Handle = PortalTravJob.Schedule(),
            PathIndex = PathIndex
        };
    }
}