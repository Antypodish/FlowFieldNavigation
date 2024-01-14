﻿using System;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

[BurstCompile]
public struct UnsafeListCleaningJob<T> : IJob where T : unmanaged
{
    public UnsafeList<T> List;
    public void Execute()
    {
        T defaultValue = default(T);
        for (int i = 0; i < List.Length; i++)
        {
            List[i] = defaultValue;
        }
    }
}