using Unity.Collections.LowLevel.Unsafe;

public struct IntegrationFieldSector
{
    public UnsafeList<IntegrationTile> integrationSector;
    public int sectorIndex1d;

    public IntegrationFieldSector(int index)
    {
        sectorIndex1d = index;
        integrationSector = new UnsafeList<IntegrationTile>();
    }
}
