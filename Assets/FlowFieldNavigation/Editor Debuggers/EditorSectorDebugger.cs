#if (UNITY_EDITOR) 

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

public class EditorSectorDebugger
{
    PathfindingManager _pathfindingManager;

    public EditorSectorDebugger(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
    }

    public void DebugSectors(int offset)
    {
        Gizmos.color = Color.black;
        NativeArray<SectorNode> sectorNodes = _pathfindingManager.FieldManager.GetFieldGraphWithOffset(offset).SectorNodes;
        float tileSize = FlowFieldUtilities.TileSize;
        float yOffset = .02f;
        for(int i = 0; i < sectorNodes.Length; i++)
        {
            Index2 index = sectorNodes[i].Sector.StartIndex;
            int sectorSize = sectorNodes[i].Sector.Size;
            Vector3 botLeft = new Vector3(index.C * tileSize, yOffset, index.R * tileSize);
            Vector3 botRight = new Vector3((index.C + sectorSize) * tileSize, yOffset, index.R * tileSize);
            Vector3 topLeft = new Vector3(index.C * tileSize, yOffset, (index.R + sectorSize) * tileSize);
            Vector3 topRight = new Vector3((index.C + sectorSize) * tileSize, yOffset, (index.R + sectorSize) * tileSize);
            Gizmos.DrawLine(botLeft, topLeft);
            Gizmos.DrawLine(topLeft, topRight);
            Gizmos.DrawLine(topRight, botRight);
            Gizmos.DrawLine(botRight, botLeft);
        }
    }
}
#endif
