using Unity.Mathematics;

public struct LocalIndex2d
{
    public int2 index;
    public int2 sector;

    public LocalIndex2d(int2 localIndex, int2 sectorIndex)
    {
        index = localIndex;
        sector = sectorIndex;
    }
}