#if (UNITY_EDITOR) 

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

internal class EditorSectorDebugger
{
    PathfindingManager _pathfindingManager;

    internal EditorSectorDebugger(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
    }

    internal void DebugSectors(int offset)
    {
        Gizmos.color = Color.black;
        NativeArray<SectorNode> sectorNodes = _pathfindingManager.FieldDataContainer.GetFieldGraphWithOffset(offset).SectorNodes;
        float yOffset = .02f;
        for(int i = 0; i < sectorNodes.Length; i++)
        {
            int2 sector2d = FlowFieldUtilities.To2D(i, FlowFieldUtilities.SectorMatrixColAmount);
            float sectorSize = FlowFieldUtilities.SectorColAmount * FlowFieldUtilities.TileSize;
            float2 sectorPos = FlowFieldUtilities.IndexToPos(sector2d, sectorSize, FlowFieldUtilities.FieldGridStartPosition);
            float2 botLeft2 = sectorPos + new float2(-sectorSize / 2, -sectorSize / 2);
            float2 topLeft2 = sectorPos + new float2(-sectorSize / 2, sectorSize / 2);
            float2 botRight2 = sectorPos + new float2(sectorSize / 2, -sectorSize / 2);
            float2 topRight2 = sectorPos + new float2(sectorSize / 2, sectorSize / 2);
            float3 botLeft3 = new float3(botLeft2.x, yOffset, botLeft2.y);
            float3 topLeft3 = new float3(topLeft2.x, yOffset, topLeft2.y);
            float3 botRight3 = new float3(botRight2.x, yOffset, botRight2.y);
            float3 topRight3 = new float3(topRight2.x, yOffset, topRight2.y);
            Gizmos.DrawLine(botLeft3, topLeft3);
            Gizmos.DrawLine(topLeft3, topRight3);
            Gizmos.DrawLine(topRight3, botRight3);
            Gizmos.DrawLine(botRight3, botLeft3);
        }
    }
}
#endif
