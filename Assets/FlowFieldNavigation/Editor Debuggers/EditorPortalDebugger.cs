#if (UNITY_EDITOR)

using UnityEngine;
using Unity.Collections;
using UnityEditor;
using Unity.Collections.LowLevel.Unsafe;


internal class EditorPortalDebugger
{
    PathfindingManager _pathfindingManager;

    SectorNode _clickedSectorNodes;
    PortalNode _clickedPortalNode;
    internal EditorPortalDebugger(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
    }
    
    internal void DebugPortals(int offset)
    {
        Gizmos.color = Color.cyan;
        FieldGraph fieldGraph = _pathfindingManager.FieldDataContainer.GetFieldGraphWithOffset(offset);
        NativeArray<WindowNode> windowNodes = fieldGraph.WindowNodes;
        NativeArray<PortalNode> portalNodes = fieldGraph.PortalNodes;
        for(int i = 0; i < windowNodes.Length; i++)
        {
            int porPtr = windowNodes[i].PorPtr;
            int porCnt = windowNodes[i].PorCnt;
            for(int j = 0; j < porCnt; j++)
            {
                PortalNode pickedPortalNode = portalNodes[porPtr + j];
                if (pickedPortalNode.Portal1.Index == pickedPortalNode.Portal2.Index) { continue; }
                Gizmos.DrawCube(pickedPortalNode.GetPosition(FlowFieldUtilities.TileSize), Vector3.one / 4);
            }
        }
    }
}
#endif
