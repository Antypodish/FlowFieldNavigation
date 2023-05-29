using JetBrains.Annotations;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

[BurstCompile]
public struct IntFieldPrepJob : IJobParallelFor
{
    public int FieldColAmount;
    public int FieldRowAmount;
    public int SectorTileAmount;
    public int SectorMatrixColAmount;
    public NativeArray<IntegrationTile> IntegrationField;
    [ReadOnly] public NativeArray<bool> SectorMarks;
    [ReadOnly] public NativeArray<byte> Costs;
    [ReadOnly] public NativeArray<SectorNode> SectorNodes;
    [ReadOnly] public NativeList<int> PickedSectors;
    public void Execute(int index)
    {   
        int sectorIndex = (index / FieldColAmount / SectorTileAmount * SectorMatrixColAmount) + (index % FieldColAmount / SectorTileAmount);
        if (SectorMarks[sectorIndex])
        {
            IntegrationTile intTile;
            intTile.Mark = IntegrationMark.Relevant;
            intTile.Cost = float.MaxValue;
            IntegrationField[index] = intTile;
        }
    }
}
