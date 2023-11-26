using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;

[BurstCompile]
public struct LOSCleanJob : IJob
{
    public float MaxLOSRange;
    public int SectorColAmount;
    public void Execute()
    {
        throw new System.NotImplementedException();
    }
}