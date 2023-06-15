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
public struct IntFieldResetJob : IJobParallelFor
{
    public UnsafeList<IntegrationTile> IntegrationFieldSector;

    IntegrationTile resetTile;
    public IntFieldResetJob(UnsafeList<IntegrationTile> integrationFieldSector)
    {
        IntegrationFieldSector = integrationFieldSector;
        resetTile = new IntegrationTile(float.MaxValue, IntegrationMark.None);
    }
    public void Execute(int index)
    {
        IntegrationFieldSector[index] = resetTile;
    }
}
