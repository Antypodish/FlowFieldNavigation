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
        for(int i = 0; i < costField.SectorGraph.SectorNodes.Length; i++)
        {
            Index2 index = costField.SectorGraph.SectorNodes[i].Sector.StartIndex;
            int size = costField.SectorGraph.SectorNodes[i].Sector.Size;
            Vector3 pos = new Vector3(index.C * tileSize + tileSize / 2, 0f, index.R * tileSize + tileSize / 2);
            Gizmos.DrawCube(pos, Vector3.one / 4);
        }
    }
}
