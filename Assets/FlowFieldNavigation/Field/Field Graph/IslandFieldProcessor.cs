using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;

internal struct IslandFieldProcessor
{
    internal float TileSize;
    internal int SectorColAmount;
    internal int SectorMatrixColAmount;
    internal int FieldColAmount;

    [ReadOnly] internal UnsafeListReadOnly<SectorNode> SectorNodes;
    [ReadOnly] internal UnsafeListReadOnly<PortalNode> PortalNodes;
    [ReadOnly] internal UnsafeListReadOnly<UnsafeList<int>> IslandFields;
    internal int GetIsland(float2 pos)
    {
        int2 index2d = FlowFieldUtilities.PosTo2D(pos, TileSize);
        int2 sector2d = FlowFieldUtilities.GetSector2D(index2d, SectorColAmount);
        int sector1d = FlowFieldUtilities.To1D(sector2d, SectorMatrixColAmount);
        SectorNode sector = SectorNodes[sector1d];

        if (sector.IsIslandValid())
        {
            return PortalNodes[sector.SectorIslandPortalIndex].IslandIndex;
        }
        else if (sector.IsIslandField)
        {
            int2 sectorStart = FlowFieldUtilities.GetSectorStartIndex(sector2d, SectorColAmount);
            int2 local2d = FlowFieldUtilities.GetLocal2D(index2d, sectorStart);
            int local1d = FlowFieldUtilities.To1D(local2d, SectorColAmount);
            int island = IslandFields[sector1d][local1d];
            switch (island)
            {
                case < 0:
                    return -island;
                case int.MaxValue:
                    return int.MaxValue;
                default:
                    return PortalNodes[island].IslandIndex;
            }
        }
        return int.MaxValue;
    }
    internal int GetIsland(int2 generalIndex2d)
    {
        int2 sector2d = FlowFieldUtilities.GetSector2D(generalIndex2d, SectorColAmount);
        int sector1d = FlowFieldUtilities.To1D(sector2d, SectorMatrixColAmount);
        SectorNode sector = SectorNodes[sector1d];

        if (sector.IsIslandValid())
        {
            return PortalNodes[sector.SectorIslandPortalIndex].IslandIndex;
        }
        else if (sector.IsIslandField)
        {
            int2 sectorStart = FlowFieldUtilities.GetSectorStartIndex(sector2d, SectorColAmount);
            int2 local2d = FlowFieldUtilities.GetLocal2D(generalIndex2d, sectorStart);
            int local1d = FlowFieldUtilities.To1D(local2d, SectorColAmount);
            int island = IslandFields[sector1d][local1d];
            switch (island)
            {
                case < 0:
                    return -island;
                case int.MaxValue:
                    return int.MaxValue;
                default:
                    return PortalNodes[island].IslandIndex;
            }
        }
        return int.MaxValue;
    }
    internal int GetIsland(int sectorIndex, int localIndex)
    {
        SectorNode sector = SectorNodes[sectorIndex];

        if (sector.IsIslandValid())
        {
            return PortalNodes[sector.SectorIslandPortalIndex].IslandIndex;
        }
        else if (sector.IsIslandField)
        {
            int island = IslandFields[sectorIndex][localIndex];
            switch (island)
            {
                case < 0:
                    return -island;
                case int.MaxValue:
                    return int.MaxValue;
                default:
                    return PortalNodes[island].IslandIndex;
            }
        }
        return int.MaxValue;
    }
    internal bool GetIslandIfNotField(int sector1d, out int islandOut)
    {
        SectorNode sector = SectorNodes[sector1d];

        if (sector.IsIslandValid())
        {
            islandOut = PortalNodes[sector.SectorIslandPortalIndex].IslandIndex;
            return true;
        }
        islandOut = 0;
        return false;
    }
}
