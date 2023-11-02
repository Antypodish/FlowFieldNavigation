using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

public class EditorIslandDebugger
{
    PathfindingManager _pathfindingManager;
    Color[] _colors;

    public EditorIslandDebugger(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
        _colors = new Color[]{
            new Color(0,0,0),
            new Color(1,0,0),
            new Color(0,1,0),
            new Color(1,1,0),
            new Color(0,0,1),
            new Color(1,0,1),
            new Color(0,1,1),
            new Color(1,1,1),
        };
    }

    public void Debug(int offset)
    {
        FieldGraph fieldGraph = _pathfindingManager.FieldProducer.GetFieldGraphWithOffset(offset);
        NativeArray<PortalNode> portalNodes = fieldGraph.PortalNodes;
        NativeArray<IslandData> islandDataList = fieldGraph.IslandDataList;
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
                Vector3 pickedPos = pickedPortalNode.GetPosition(FlowFieldUtilities.TileSize);
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
                Gizmos.DrawLine(pickedPos, neighbourNode.GetPosition(FlowFieldUtilities.TileSize));
            }
        }
    }
}
