using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

[BurstCompile]
public struct IntFieldResetJob : IJobParallelFor
{
    public NativeArray<IntegrationTile> IntegrationField;

    IntegrationTile resetTile;
    public IntFieldResetJob(NativeArray<IntegrationTile> integrationField)
    {
        IntegrationField = integrationField;
        resetTile = new IntegrationTile(float.MaxValue, IntegrationMark.Irrelevant);
    }
    public void Execute(int index)
    {
        IntegrationField[index] = resetTile;
    }
}
