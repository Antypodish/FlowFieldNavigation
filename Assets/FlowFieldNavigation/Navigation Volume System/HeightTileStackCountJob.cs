using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;
[BurstCompile]
internal struct HeightTileStackCountJob : IJobParallelFor
{
    internal int SecCompVoxCount;
    internal int XSecCount;
    internal int ZSecCount;
    [ReadOnly] internal NativeHashMap<int, UnsafeBitArray> SectorBits;
    internal NativeArray<HeightTile> HeightTiles;
    public void Execute(int index)
    {
        HeightTile tile = HeightTiles[index];
        int3 startIndex = tile.VoxIndex;
        if (startIndex.Equals(int.MinValue)) { return; }

        int3 sector = FlowFieldVolumeUtilities.GetSector(startIndex, SecCompVoxCount);
        int3 localIndex = FlowFieldVolumeUtilities.GetLocal3d(startIndex, sector, SecCompVoxCount);
        int sector1d = FlowFieldVolumeUtilities.To1D(sector, XSecCount, ZSecCount);
        int count = 0;
        bool failed = false;
        while (SectorBits.TryGetValue(sector1d, out UnsafeBitArray bits) && !failed)
        {
            for(int y = localIndex.y; y >= 0; y--)
            {
                int3 new3d = new int3(localIndex.x, y, localIndex.z);
                int new1d = FlowFieldVolumeUtilities.To1D(new3d, SecCompVoxCount, SecCompVoxCount);
                if (bits.IsSet(new1d))
                {
                    count++;
                    continue;
                }
                failed = true;
                break;
            }
            sector = new int3(sector.x, sector.y - 1, sector.z);
            sector1d = FlowFieldVolumeUtilities.To1D(sector, XSecCount, ZSecCount);
            localIndex = new int3(localIndex.x, SecCompVoxCount - 1, localIndex.z);
        }
        tile.StackCount = count;
        HeightTiles[index] = tile;
    }
}