internal struct LocalIndex1d
{
    internal int index;
    internal int sector;

    internal LocalIndex1d(int localIndex, int sectorIndex)
    {
        index = localIndex;
        sector = sectorIndex;
    }
}
