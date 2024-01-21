using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

[BurstCompile]
internal struct UnsafeListResetJob<T> : IJob where T : unmanaged
{
    internal UnsafeList<T> List;
    public void Execute()
    {
        for(int i = 0; i < List.Length; i++)
        {
            List[i] = default(T);
        }
    }
}
