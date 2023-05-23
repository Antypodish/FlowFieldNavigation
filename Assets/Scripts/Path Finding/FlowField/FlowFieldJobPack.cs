using Unity.Collections;
using Unity.Jobs;

public struct FlowFieldJobPack
{
    public Path Path;
    public FieldGraphTraversalJob TraversalJob;
    public IntFieldResetJob ResetJob;
    public IntFieldPrepJob PrepJob;
    public IntFieldJob IntegrationJob;
    public FlowFieldJob FlowFieldJob;

    public JobHandle SchedulePack()
    {
        JobHandle traversalJobHandle = TraversalJob.Schedule();
        JobHandle resetJobHandle = ResetJob.Schedule(ResetJob.IntegrationField.Length, 512, traversalJobHandle);
        JobHandle prepHandle = PrepJob.Schedule(PrepJob.IntegrationField.Length, 1024, resetJobHandle);
        JobHandle integrationJobHandle = IntegrationJob.Schedule(prepHandle);
        JobHandle flowFieldJobHandle = FlowFieldJob.Schedule(FlowFieldJob.FlowField.Length, 512, integrationJobHandle);
        return flowFieldJobHandle;
    }
}
