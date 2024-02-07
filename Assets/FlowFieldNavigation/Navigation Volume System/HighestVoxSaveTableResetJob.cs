using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
[BurstCompile]
internal struct HighestVoxSaveTableResetJob : IJob
{
    [WriteOnly] internal NativeArray<HeightTile> Table;
    public void Execute()
    {
        for(int i = 0; i < Table.Length; i++)
        {
            Table[i] = new HeightTile()
            {
                StackCount = 0,
                VoxIndex = new int3(int.MinValue, int.MinValue, int.MinValue),
            };
        }
    }
}
