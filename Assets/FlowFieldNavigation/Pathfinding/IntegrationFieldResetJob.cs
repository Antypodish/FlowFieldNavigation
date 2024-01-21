using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

[BurstCompile]
internal struct IntegrationFieldResetJob : IJob
{
    internal int StartIndex;
    internal NativeArray<IntegrationTile> IntegrationField;

    public void Execute()
    {
        for(int i = StartIndex; i < IntegrationField.Length; i++)
        {
            IntegrationField[i] = new IntegrationTile(float.MaxValue, IntegrationMark.None);
        }
    }
}
