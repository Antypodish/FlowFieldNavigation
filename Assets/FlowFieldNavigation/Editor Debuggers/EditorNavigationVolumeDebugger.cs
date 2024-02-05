using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Unity.Mathematics;
using UnityEngine;
using Unity.Collections;
using static UnityEditor.PlayerSettings;
using UnityEditor;
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
    internal void DebugNavigationSurfaceVolume()
    {
        Gizmos.color = new Color(1, 0, 0, 0.5f);
        int xVoxCount = FlowFieldVolumeUtilities.XAxisVoxelCount;
        int yVoxCount = FlowFieldVolumeUtilities.YAxisVoxelCount;
        int zVoxCount = FlowFieldVolumeUtilities.ZAxisVoxelCount;
        float voxHorSize = FlowFieldVolumeUtilities.VoxelHorizontalSize;
        float voxVerSize = FlowFieldVolumeUtilities.VoxelVerticalSize;
        float3 volumeStartPos = FlowFieldVolumeUtilities.VolumeStartPos;
        NativeBitArray surfaceVolume = _pathfindingManager.FieldDataContainer.NavigationVolumeSystem.NavigationVolumeBitArray;
        for(int i = 0; i < surfaceVolume.Length; i++)
        {
            if (!surfaceVolume.IsSet(i)) { continue; }
            int3 index = FlowFieldVolumeUtilities.To3D(i, xVoxCount, yVoxCount, zVoxCount);
            float3 pos = FlowFieldVolumeUtilities.GetVoxelCenterPos(index, volumeStartPos, voxHorSize, voxVerSize);
            float3 size = new float3(voxHorSize, voxVerSize, voxHorSize);
            Gizmos.DrawCube(pos, size);
        }
    }
}