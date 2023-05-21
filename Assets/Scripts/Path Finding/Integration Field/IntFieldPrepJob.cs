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
        /*
        Sector sector = SectorNodes[PickedSectors[index]].Sector;
        Index2 start = sector.StartIndex;
        int sectorSize = sector.Size;
        int botLeftInclusive = start.R * FieldColAmount + start.C;
        int topLeftExclusive = botLeftInclusive + (sectorSize * FieldColAmount);


        for(int r = botLeftInclusive; r < topLeftExclusive; r+=FieldColAmount)
        {
            for(int i = r; i < r + sectorSize; i++)
            {
                IntegrationMark mark = Costs[i] == byte.MaxValue ? IntegrationMark.Absolute : IntegrationMark.None;
                IntegrationTile intTile;
                intTile.Mark = mark;
                intTile.Cost = int.MaxValue;
                IntegrationField[i] = intTile;
            }
        }*/
        
        int sectorIndex = (index / FieldColAmount / SectorTileAmount * SectorMatrixColAmount) + (index % FieldColAmount / SectorTileAmount);
        if (SectorMarks[sectorIndex])
        {
            IntegrationMark mark = Costs[index] == byte.MaxValue ? IntegrationMark.Absolute : IntegrationMark.None;
            IntegrationTile intTile;
            intTile.Mark = mark;
            intTile.Cost = int.MaxValue;
            IntegrationField[index] = intTile;
        }
    }
}
