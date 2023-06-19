using System.Diagnostics;
using Unity.Collections;
using Unity.Jobs;

public struct FlowFieldJobPack
{
    public Path Path;
    public FieldGraphTraversalJob TraversalJob;
    public IntFieldResetJob ResetJob;
    public LOSJob LOSJob;
    public IntFieldJob IntegrationJob;
    public FlowFieldJob FlowFieldJob;
    public JobHandle SchedulePack()
    {
        JobHandle traversalJobHandle = TraversalJob.Schedule();
        JobHandle resetJobHandle = ResetJob.Schedule(ResetJob.IntegrationFieldSector.Length, 512, traversalJobHandle);
        JobHandle losHandle = LOSJob.Schedule(traversalJobHandle);
        JobHandle integrationJobHandle = IntegrationJob.Schedule(losHandle);
        JobHandle flowFieldJobHandle = FlowFieldJob.Schedule(FlowFieldJob.FlowSector.Length, 512, integrationJobHandle);
        return flowFieldJobHandle;
    }
}
