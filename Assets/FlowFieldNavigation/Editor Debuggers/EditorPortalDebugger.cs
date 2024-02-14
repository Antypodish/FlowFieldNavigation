#if (UNITY_EDITOR)

using UnityEngine;
using Unity.Collections;
using UnityEditor;
using Unity.Collections.LowLevel.Unsafe;
using System.Collections.Generic;

internal class EditorPortalDebugger
{
    FlowFieldNavigationManager _navigationManager;
    PortalDebugMeshBuilder _portalDebugMeshBuilder;
    internal EditorPortalDebugger(FlowFieldNavigationManager navigationManager)
    {
        _navigationManager = navigationManager;
        _portalDebugMeshBuilder = new PortalDebugMeshBuilder(navigationManager);
    }
    
    internal void DebugPortals(int offset)
    {
        Gizmos.color = Color.cyan;
        List<Mesh> meshesToDebug = _portalDebugMeshBuilder.GetDebugMeshes(offset);
        for(int i = 0; i < meshesToDebug.Count; i++)
        {
            Gizmos.DrawMesh(meshesToDebug[i]);
        }
    }
    /*
    internal void DebugPortalIslands(int offset)
    {
        FieldGraph fieldGraph = _navigationManager.FieldDataContainer.GetFieldGraphWithOffset(offset);
        NativeArray<PortalNode> portalNodes = fieldGraph.PortalNodes;
        NativeArray<PortalToPortal> portalToPortals = fieldGraph.PorToPorPtrs;
        NativeArray<WindowNode> windowNodes = fieldGraph.WindowNodes;
        for (int i = 0; i < windowNodes.Length; i++)
        {
            int porPtr = windowNodes[i].PorPtr;
            int porCnt = windowNodes[i].PorCnt;
            for (int j = 0; j < porCnt; j++)
            {
                PortalNode pickedPortalNode = portalNodes[porPtr + j];
                if (pickedPortalNode.Portal1.Index == pickedPortalNode.Portal2.Index) { continue; }
                Gizmos.color = _colors[pickedPortalNode.IslandIndex % _colors.Length];
                Vector3 pickedPos = pickedPortalNode.GetPosition(FlowFieldUtilities.TileSize, FlowFieldUtilities.FieldGridStartPosition);
                Gizmos.DrawCube(pickedPos, new Vector3(0.5f, 0.5f, 0.5f));
                DebugNeighboursOf(pickedPortalNode.Portal1, pickedPos);
                DebugNeighboursOf(pickedPortalNode.Portal2, pickedPos);
            }
        }

        void DebugNeighboursOf(Portal portal, Vector3 pickedPos)
        {
            int porToPorPtr = portal.PorToPorPtr;
            for (int i = 0; i < portal.PorToPorCnt; i++)
            {
                int index = portalToPortals[porToPorPtr + i].Index;
                PortalNode neighbourNode = portalNodes[index];
                Gizmos.DrawLine(pickedPos, neighbourNode.GetPosition(FlowFieldUtilities.TileSize, FlowFieldUtilities.FieldGridStartPosition));
            }
        }
    }*/
}
#endif
