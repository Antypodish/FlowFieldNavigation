using Unity.Mathematics;

namespace FlowFieldNavigation
{
    internal static class FlowFieldVolumeUtilities
    {
        internal static float3 VolumeStartPos;
        internal static int YAxisVoxelCount;
        internal static int XAxisVoxelCount;
        internal static int ZAxisVoxelCount;
        internal static float VoxelHorizontalSize;
        internal static float VoxelVerticalSize;
        internal static int SectorComponentVoxelCount;
        internal static int XAxisSectorCount;
        internal static int YAxisSectorCount;
        internal static int ZAxisSectorCount;
        internal static float SectorHorizontalSize;
        internal static float SectorVerticalSize;

        internal static int3 PosToIndex(float3 pos, float3 volumeStartPos, float voxelHorizontalSize, float voxelVerticalSize)
        {
            pos -= volumeStartPos;
            return (int3)math.floor(pos / new float3(voxelHorizontalSize, voxelVerticalSize, voxelHorizontalSize));
        }
        internal static int To1D(int3 index, int xVoxCount, int zVoxCount)
        {
            return (index.y * xVoxCount * zVoxCount) + (index.z * xVoxCount) + index.x;
        }
        internal static int3 To3D(int index, int xVoxCount, int zVoxCount)
        {
            int y = index / (xVoxCount * zVoxCount);
            int z = index % (xVoxCount * zVoxCount) / xVoxCount;
            int x = index % xVoxCount;
            return new int3(x, y, z);
        }
        internal static int3 GetSector(int3 index, int sectorComponentVoxelCount)
        {
            return index / sectorComponentVoxelCount;
        }
        internal static int3 GetLocal3d(int3 generalIndex, int3 sectorIndex, int sectorComponentVoxCount)
        {
            int3 sectorStart3 = sectorIndex * sectorComponentVoxCount;
            return generalIndex - sectorStart3;
        }
        internal static LocalIndex1d GetLocal1D(int3 generalIndex, int sectorComponentVoxCount, int xSecCount, int zSecCount)
        {
            int3 sector3 = generalIndex / sectorComponentVoxCount;
            int3 sectorStart3 = sector3 * sectorComponentVoxCount;
            int3 local3 = generalIndex - sectorStart3;
            return new LocalIndex1d()
            {
                sector = To1D(sector3, xSecCount, zSecCount),
                index = To1D(local3, sectorComponentVoxCount, sectorComponentVoxCount),
            };
        }
        internal static int3 GetGeneral3D(int sector1d, int local1, int sectorComponentVoxCount, int xSecCount, int zSecCount)
        {
            int3 sector3 = To3D(sector1d, xSecCount, zSecCount);
            int3 sectorStart3 = sector3 * sectorComponentVoxCount;
            int3 local3 = To3D(local1, sectorComponentVoxCount, sectorComponentVoxCount);
            return sectorStart3 + local3;
        }
        internal static int3 Clamp(int3 index, int xVoxCount, int yVoxCount, int zVoxCount)
        {
            index.x = math.max(index.x, 0);
            index.y = math.max(index.y, 0);
            index.z = math.max(index.z, 0);
            index.x = math.min(index.x, xVoxCount - 1);
            index.y = math.min(index.y, yVoxCount - 1);
            index.z = math.min(index.z, zVoxCount - 1);
            return index;
        }
        internal static bool WithinBounds(int3 index, int xVoxCount, int yVoxCount, int zVoxCount)
        {
            float3 maxs = new float3(xVoxCount, yVoxCount, zVoxCount);
            bool3 check = index < maxs & index >= 0;
            return check.x & check.y & check.z;
        }
        internal static float3 GetVoxelStartPos(int3 index, float3 volmeStartPos, float voxHorSize, float voxVerSize)
        {
            return volmeStartPos + (index * new float3(voxHorSize, voxVerSize, voxHorSize));
        }
        internal static float3 GetVoxelCenterPos(int3 index, float3 volmeStartPos, float voxHorSize, float voxVerSize)
        {
            return volmeStartPos + (index * new float3(voxHorSize, voxVerSize, voxHorSize)) + new float3(voxHorSize / 2, voxVerSize / 2, voxHorSize / 2);
        }
    }

}
