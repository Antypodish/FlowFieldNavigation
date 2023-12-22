using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

[BurstCompile]
public struct UnsafeListResetJob<T> : IJob where T : unmanaged
{
    public UnsafeList<T> List;
    public void Execute()
    {
        for(int i = 0; i < List.Length; i++)
        {
            List[i] = default(T);
        }
    }
}
