using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

[BurstCompile]
public struct IntegrationFieldExtensionJob : IJob
{
    public int oldFieldLength;
    [WriteOnly] public NativeArray<IntegrationTile> NewIntegrationField;
    public void Execute()
    {
        for (int i = oldFieldLength; i < NewIntegrationField.Length; i++)
        {
            NewIntegrationField[i] = new IntegrationTile(float.MaxValue, IntegrationMark.None);
        }
    }
}