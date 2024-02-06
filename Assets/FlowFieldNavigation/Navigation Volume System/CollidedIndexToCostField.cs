using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;
[BurstCompile]

internal struct CollidedIndexToCostField : IJob
{
    [ReadOnly] internal NativeList<int3> CollidedIndicies;
    [WriteOnly] NativeArray<byte> Costs;
    public void Execute()
    {
        //Make everywhere walkable
        for(int i = 0; i < Costs.Length; i++)
        {
            Costs[i] = 1;
        }
    }
}
