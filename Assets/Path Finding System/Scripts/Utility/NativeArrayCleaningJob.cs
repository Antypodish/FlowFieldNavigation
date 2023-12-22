using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

[BurstCompile]
public struct NativeArrayCleaningJob<T> : IJob where T : unmanaged
{
    public NativeArray<T> Array;
    public void Execute()
    {
        T defaultValue = default(T);
        for(int i = 0; i < Array.Length; i++)
        {
            Array[i] = defaultValue;
        }
    }
}
