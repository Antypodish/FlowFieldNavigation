using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

[BurstCompile]
public struct UnsafeListDefaultSetterJob<T> : IJob where T : unmanaged
{
    [WriteOnly] public UnsafeList<T> List;
    public void Execute()
    {
        T def = default(T);
        for(int i = 0; i < List.Length; i++)
        {
            List[i] = def;
        }
    }
}