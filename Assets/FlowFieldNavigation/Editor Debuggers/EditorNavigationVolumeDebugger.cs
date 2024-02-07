using Unity.Mathematics;
using UnityEngine;
using Unity.Collections;
using UnityEditor;
using Unity.Collections.LowLevel.Unsafe;
internal class EditorNavigationVolumeDebugger
{
    PathfindingManager _pathfindingManager;
    internal EditorNavigationVolumeDebugger(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
    }

    internal void DebugVolumeBoundaries()
    {
        float3 volumeStartPos = FlowFieldVolumeUtilities.VolumeStartPos;
        int xAxisVoxelCounts = FlowFieldVolumeUtilities.XAxisVoxelCount;
        int yAxisVoxelCounts = FlowFieldVolumeUtilities.YAxisVoxelCount;
        int zAxisVoxelCounts = FlowFieldVolumeUtilities.ZAxisVoxelCount;
        float voxelHorizontalSize = FlowFieldVolumeUtilities.VoxelHorizontalSize;
        float voxelVerticalSize = FlowFieldVolumeUtilities.VoxelVerticalSize;

        Gizmos.color = Color.white;
        float3 lbl = volumeStartPos;
        float3 ltl = volumeStartPos + new float3(0, 0, voxelHorizontalSize * zAxisVoxelCounts);
        float3 ltr = volumeStartPos + new float3(voxelHorizontalSize * xAxisVoxelCounts, 0, voxelHorizontalSize * zAxisVoxelCounts);
        float3 lbr = volumeStartPos + new float3(voxelHorizontalSize * xAxisVoxelCounts, 0, 0);
        float3 ubl = lbl + new float3(0, yAxisVoxelCounts * voxelVerticalSize, 0);
        float3 utl = ltl + new float3(0, yAxisVoxelCounts * voxelVerticalSize, 0);
        float3 utr = ltr + new float3(0, yAxisVoxelCounts * voxelVerticalSize, 0);
        float3 ubr = lbr + new float3(0, yAxisVoxelCounts * voxelVerticalSize, 0);

        Gizmos.DrawLine(lbl, ltl);
        Gizmos.DrawLine(ltl, ltr);
        Gizmos.DrawLine(ltr, lbr);
        Gizmos.DrawLine(lbr, lbl);

        Gizmos.DrawLine(ubl, utl);
        Gizmos.DrawLine(utl, utr);
        Gizmos.DrawLine(utr, ubr);
        Gizmos.DrawLine(ubr, ubl);

        Gizmos.DrawLine(lbl, ubl);
        Gizmos.DrawLine(ltl, utl);
        Gizmos.DrawLine(ltr, utr);
        Gizmos.DrawLine(lbr, ubr);
    }
    internal void DebugVolumeSectorBounds()
    {
        Gizmos.color = Color.white;
        float3 volumeStartPos = FlowFieldVolumeUtilities.VolumeStartPos;
        int xAxisSectorCount = FlowFieldVolumeUtilities.XAxisSectorCount;
        int yAxisSectorCount = FlowFieldVolumeUtilities.YAxisSectorCount;
        int zAxisSectorCount = FlowFieldVolumeUtilities.ZAxisSectorCount;
        float secHorSize = FlowFieldVolumeUtilities.SectorHorizontalSize;
        float secVerSize = FlowFieldVolumeUtilities.SectorVerticalSize;
        for(int x = 0; x < xAxisSectorCount; x++)
        {
            for (int z = 0; z < zAxisSectorCount; z++)
            {
                for (int y = 0; y < yAxisSectorCount; y++)
                {
                    int3 sectorIndex = new int3(x, y, z);
                    float3 secPos = FlowFieldVolumeUtilities.GetVoxelCenterPos(sectorIndex, volumeStartPos, secHorSize, secVerSize);
                    Gizmos.DrawWireCube(secPos, new Vector3(secHorSize, secVerSize, secHorSize));
                }
            }
        }
    }
    internal void DebugVolumeDetectedSectors()
    {
        Gizmos.color = new Color(0, 1, 1, 0.5f);
        float3 volumeStartPos = FlowFieldVolumeUtilities.VolumeStartPos;
        int xAxisSectorCount = FlowFieldVolumeUtilities.XAxisSectorCount;
        int yAxisSectorCount = FlowFieldVolumeUtilities.YAxisSectorCount;
        int zAxisSectorCount = FlowFieldVolumeUtilities.ZAxisSectorCount;
        float secHorSize = FlowFieldVolumeUtilities.SectorHorizontalSize;
        float secVerSize = FlowFieldVolumeUtilities.SectorVerticalSize;
        NativeHashMap<int, UnsafeBitArray> volumeBits = _pathfindingManager.FieldDataContainer.NavigationVolumeSystem.SurfaceVolumeBits;
        NativeArray<int> sectors = volumeBits.GetKeyArray(Allocator.Temp);
        for(int i = 0; i < sectors.Length; i++)
        {
            int currentSector = sectors[i];
            int3 index3 = FlowFieldVolumeUtilities.To3D(currentSector, xAxisSectorCount, zAxisSectorCount);
            float3 secPos = FlowFieldVolumeUtilities.GetVoxelCenterPos(index3, volumeStartPos, secHorSize, secVerSize);
            Gizmos.DrawCube(secPos, new Vector3(secHorSize, secVerSize, secHorSize));

        }
    }
    internal void DebugNavigationSurfaceVolume()
    {
        Gizmos.color = new Color(1, 0, 0, 1f);
        float voxHorSize = FlowFieldVolumeUtilities.VoxelHorizontalSize;
        float voxVerSize = FlowFieldVolumeUtilities.VoxelVerticalSize;
        int sectorComponentVoxCount = FlowFieldVolumeUtilities.SectorComponentVoxelCount;
        int xSecCount = FlowFieldVolumeUtilities.XAxisSectorCount;
        int zSecCount = FlowFieldVolumeUtilities.ZAxisSectorCount;
        float3 volumeStartPos = FlowFieldVolumeUtilities.VolumeStartPos;
        NativeHashMap<int, UnsafeBitArray> sectorBits = _pathfindingManager.FieldDataContainer.NavigationVolumeSystem.SurfaceVolumeBits;
        NativeHashMap<int, UnsafeBitArray>.Enumerator sectorBitsEnumerator = sectorBits.GetEnumerator();
        while (sectorBitsEnumerator.MoveNext())
        {
            KVPair<int, UnsafeBitArray> sectorBitsPair = sectorBitsEnumerator.Current;
            int sector1d = sectorBitsPair.Key;
            UnsafeBitArray bits = sectorBitsPair.Value;
            for(int i = 0; i < bits.Length; i++)
            {
                if (!bits.IsSet(i)) { continue; }
                int3 sector3d = FlowFieldVolumeUtilities.GetGeneral3D(sector1d, i, sectorComponentVoxCount, xSecCount, zSecCount);
                float3 pos = FlowFieldVolumeUtilities.GetVoxelCenterPos(sector3d, volumeStartPos, voxHorSize, voxVerSize);
                float3 size = new float3(voxHorSize, voxVerSize, voxHorSize);
                Gizmos.DrawCube(pos, size);
            }
        }
    }
    internal void DebugHighestVoxels()
    {
        Gizmos.color = Color.blue;
        float voxHorSize = FlowFieldVolumeUtilities.VoxelHorizontalSize;
        float voxVerSize = FlowFieldVolumeUtilities.VoxelVerticalSize;
        int sectorComponentVoxCount = FlowFieldVolumeUtilities.SectorComponentVoxelCount;
        int xSecCount = FlowFieldVolumeUtilities.XAxisSectorCount;
        int zSecCount = FlowFieldVolumeUtilities.ZAxisSectorCount;
        float3 volumeStartPos = FlowFieldVolumeUtilities.VolumeStartPos;
        NativeArray<HeightTile> highestVoxels = _pathfindingManager.FieldDataContainer.NavigationVolumeSystem.HighestVoxelSaveTable;
        for(int i = 0; i < highestVoxels.Length; i++)
        {
            HeightTile heightTile = highestVoxels[i];
            if(heightTile.VoxIndex.y == int.MinValue) { continue; }
            for(int j = 0; j < heightTile.StackCount; j++)
            {
                int3 voxel = heightTile.VoxIndex;
                voxel.y -= j;
                float3 pos = FlowFieldVolumeUtilities.GetVoxelCenterPos(voxel, volumeStartPos, voxHorSize, voxVerSize);
                float3 size = new float3(voxHorSize, voxVerSize, voxHorSize);
                Gizmos.DrawCube(pos, size);
            }
        }

    }
}