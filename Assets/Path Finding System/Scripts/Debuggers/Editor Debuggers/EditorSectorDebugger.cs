#if (UNITY_EDITOR) 

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
        CostField costField = _pathfindingManager.CostFieldProducer.GetCostFieldWithOffset(offset);
        float tileSize = _pathfindingManager.TileSize;
        float yOffset = .02f;
        for(int i = 0; i < costField.FieldGraph.SectorNodes.Length; i++)
        {
            Index2 index = costField.FieldGraph.SectorNodes[i].Sector.StartIndex;
            int sectorSize = costField.FieldGraph.SectorNodes[i].Sector.Size;
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
