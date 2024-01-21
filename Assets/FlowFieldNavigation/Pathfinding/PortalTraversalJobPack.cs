using Unity.Collections;
using Unity.Jobs;

internal struct PortalTraversalJobPack
{
    internal PortalNodeTraversalJob PortalTravJob;
    internal int PathIndex;

    internal NewPathHandle Schedule()
    {
        return new NewPathHandle()
        {
            Handle = PortalTravJob.Schedule(),
            PathIndex = PathIndex
        };
    }
}