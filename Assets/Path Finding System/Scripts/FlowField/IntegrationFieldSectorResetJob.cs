using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;

[BurstCompile]
public struct IntegrationFieldSectorResetJob : IJob
{
    public int SectorTileAmount;
    [ReadOnly] public NativeArray<int> SectorStartIndicies;
    [WriteOnly] public NativeArray<IntegrationTile> IntegrationField;
    public void Execute()
    {
        for(int i = 0; i < SectorStartIndicies.Length; i++)
        {
            int sectorStartIndex = SectorStartIndicies[i];
            for(int j = sectorStartIndex; j < sectorStartIndex + SectorTileAmount; j++)
            {
                IntegrationField[j] = new IntegrationTile()
                {
                    Cost = float.MaxValue,
                    Mark = 0,
                };
            }
        }
    }
}
