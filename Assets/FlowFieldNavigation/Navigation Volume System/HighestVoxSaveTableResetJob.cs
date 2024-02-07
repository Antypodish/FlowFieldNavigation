using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
[BurstCompile]
internal struct HighestVoxSaveTableResetJob : IJob
{
    [WriteOnly] internal NativeArray<int3> Table;
    public void Execute()
    {
        for(int i = 0; i < Table.Length; i++)
        {
            Table[i] = new int3(int.MinValue, int.MinValue, int.MinValue);
        }
    }
}
