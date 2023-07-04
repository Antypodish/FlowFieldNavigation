using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

public class NewFlowFieldJob : IJobParallelFor
{
    public UnsafeList<IntegrationFieldSector> IntegrationFieldSectors;
    public UnsafeList<FlowFieldSector> FlowFieldSectors;

    public void Execute(int index)
    {
        throw new System.NotImplementedException();
    }
}
