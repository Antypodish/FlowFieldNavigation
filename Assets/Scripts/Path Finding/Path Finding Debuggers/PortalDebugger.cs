using UnityEngine;
using Unity.Collections;

public class PortalDebugger
{
    PathfindingManager _pathfindingManager;

    public PortalDebugger(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
    }
    
    public void DebugPortals(int offset)
    {
        Gizmos.color = Color.cyan;
        NativeArray<PortalNode> portalNodes = _pathfindingManager.CostFieldProducer.GetCostFieldWithOffset(offset).SectorGraph.PortalNodes.Nodes;
        float tileSize = _pathfindingManager.TileSize;
        float yOffset = .02f;
        for(int i = 0; i < portalNodes.Length; i++)
        {
            Index2 index1 = portalNodes[i].Portal.Index1;
            Index2 index2 = portalNodes[i].Portal.Index2;

            Vector3 pos1 = new Vector3(tileSize / 2 + tileSize * index1.C, yOffset, tileSize / 2 + tileSize * index1.R);
            Vector3 pos2 = new Vector3(tileSize / 2 + tileSize * index2.C, yOffset, tileSize / 2 + tileSize * index2.R);
            Gizmos.DrawCube((pos1+pos2)/2, Vector3.one / 4);
        }
    }
}
