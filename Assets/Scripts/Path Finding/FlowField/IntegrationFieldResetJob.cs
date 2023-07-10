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
public struct IntegrationFieldResetJob : IJobParallelFor
{
    public NativeArray<IntegrationTile> IntegrationField;

    public void Execute(int index)
    {
        IntegrationField[index] = new IntegrationTile(float.MaxValue, IntegrationMark.None);
    }
}
