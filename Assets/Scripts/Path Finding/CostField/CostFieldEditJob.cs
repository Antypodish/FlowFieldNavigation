using Unity.Burst;
using Unity.Jobs;

[BurstCompile]
public struct CostFieldEditJob : IJob
{
    public Index2 Bound1;
    public Index2 Bound2;
    public FieldGraph FieldGraph;

    public CostFieldEditJob(FieldGraph fieldGraph, Index2 bound1, Index2 bound2)
    {
        FieldGraph = fieldGraph;
        Bound1 = bound1;
        Bound2 = bound2;
    }
    public void Execute()
    {
        FieldGraph.SetUnwalkable(Bound1, Bound2);
    }
}
}
