using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
[BurstCompile]
public struct DijkstraTileArrayCleaningJob : IJob
{
    [WriteOnly] public NativeArray<DijkstraTile> Array;
    public void Execute()
    {
        for(int i = 0; i < Array.Length; i++)
        {
            Array[i] = new DijkstraTile();
        }
    }
}
