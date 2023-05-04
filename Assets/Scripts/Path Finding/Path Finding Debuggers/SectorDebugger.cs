using UnityEngine;

public class SectorDebugger
{
    PathfindingManager _pathfindingManager;

    public SectorDebugger(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
    }

    public void DebugSectors(int offset)
    {
        Gizmos.color = Color.black;
        CostField costField = _pathfindingManager.CostFieldProducer.GetCostFieldWithOffset(offset);
        float tileSize = _pathfindingManager.TileSize;
        float yOffset = .02f;
        for(int i = 0; i < costField.SectorGraph.SectorNodes.Nodes.Length; i++)
        {
            Index2 index = costField.SectorGraph.SectorNodes.Nodes[i].Sector.StartIndex;
            int sectorSize = costField.SectorGraph.SectorNodes.Nodes[i].Sector.Size;
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
