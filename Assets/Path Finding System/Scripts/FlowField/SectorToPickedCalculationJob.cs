using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

[BurstCompile]
public struct SectorToPickedCalculationJob : IJob
{
    public int SectorTileAmount;
    [ReadOnly] public NativeArray<int> PickedToSector;
    [ReadOnly] public NativeArray<int> NewSectorStartIndex;
    [WriteOnly] public NativeArray<int> SectorToPicked;

    public void Execute()
    {
        for (int i = NewSectorStartIndex[0]; i < PickedToSector.Length; i++)
        {
            SectorToPicked[PickedToSector[i]] = i * SectorTileAmount + 1;
        }
    }
}
