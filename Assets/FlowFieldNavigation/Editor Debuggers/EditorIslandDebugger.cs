using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

internal class EditorIslandDebugger
{
    PathfindingManager _pathfindingManager;
    Color[] _colors;
    Mesh _debugMesh;
    Vector3[] _debugVerticies;
    int[] _debugTriangles;

    internal EditorIslandDebugger(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
        _colors = new Color[]{
            new Color(0,0,0),
            new Color(1,0,0),
            new Color(0,1,0),
            new Color(1,1,0),
            new Color(1,0,1),
            new Color(0,1,1),
            new Color(1,1,1),

            new Color(0.5f,0,0),
            new Color(0,0.5f,0),
            new Color(0.5f,0.5f,0),
            new Color(0,0,0.5f),
            new Color(0.5f,0,0.5f),
            new Color(0,0.5f,0.5f),
            new Color(0.5f,0.5f,0.5f),
        };


        //configure debug mesh
        float tileSize = 1;
        _debugMesh = new Mesh();
        _debugVerticies = new Vector3[4];
        _debugTriangles = new int[6];
        SetVerticies();
        SetTriangles();
        UpdateMesh();

        //HELPERS
        void SetVerticies()
        {
            _debugVerticies[0] = new Vector3(0, 0, tileSize);
            _debugVerticies[1] = new Vector3(tileSize, 0, 0);
            _debugVerticies[2] = new Vector3(0, 0, 0);
            _debugVerticies[3] = new Vector3(tileSize, 0, tileSize);

            _debugMesh.vertices = _debugVerticies;
        }
        void SetTriangles()
        {
            _debugTriangles[0] = 0;
            _debugTriangles[1] = 1;
            _debugTriangles[2] = 2;
            _debugTriangles[3] = 0;
            _debugTriangles[4] = 3;
            _debugTriangles[5] = 1;

            _debugMesh.triangles = _debugTriangles;
        }
        void UpdateMesh()
        {
            _debugMesh.Clear();
            _debugMesh.vertices = _debugVerticies;
            _debugMesh.triangles = _debugTriangles;
            _debugMesh.RecalculateNormals();
        }
    }

    internal void DebugPortalIslands(int offset)
    {
        FieldGraph fieldGraph = _pathfindingManager.FieldManager.GetFieldGraphWithOffset(offset);
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
    internal void DebugTileIslands(int offset)
    {
        float yOffset = 0.2f;
        float tileSize = FlowFieldUtilities.TileSize;
        FieldGraph fieldGraph = _pathfindingManager.FieldManager.GetFieldGraphWithOffset(offset);
        NativeArray<SectorNode> sectorNodes = fieldGraph.SectorNodes;
        NativeArray<byte> costsl = _pathfindingManager.FieldManager.GetCostFieldWithOffset(offset).Costs;
        NativeArray<UnsafeList<int>> islandFields = fieldGraph.IslandFields;
        NativeArray<PortalNode> portalNodes = fieldGraph.PortalNodes;
        for (int i = 0; i < sectorNodes.Length; i++)
        {
            SectorNode sector = sectorNodes[i];
            if (sector.IsIslandValid())
            {
                DebugWholeSector(i, portalNodes[sector.SectorIslandPortalIndex].IslandIndex);
            }
            else if (sector.IsIslandField)
            {
                DebugIslandFields(i, islandFields[i]);
            }
        }


        void DebugIslandFields(int sectorIndex, UnsafeList<int> islandField)
        {
            int2 sector2d = FlowFieldUtilities.To2D(sectorIndex, FlowFieldUtilities.SectorMatrixColAmount);
            for (int i = 0; i < islandField.Length; i++)
            {
                int islandIndex = islandField[i];
                if(islandIndex == int.MinValue || islandIndex == int.MaxValue) { continue; }
                if(islandIndex < 0) { islandIndex = -islandIndex; }
                else { islandIndex = portalNodes[islandIndex].IslandIndex; }
                int2 local2d = FlowFieldUtilities.To2D(i, FlowFieldUtilities.SectorColAmount);
                int2 general2d = FlowFieldUtilities.GetGeneral2d(local2d, sector2d, FlowFieldUtilities.SectorColAmount, FlowFieldUtilities.FieldColAmount);
                float3 pos = new float3(general2d.x * tileSize, yOffset, general2d.y * tileSize);

                Gizmos.color = _colors[islandIndex % _colors.Length];
                Gizmos.DrawMesh(_debugMesh, pos);
            }
        }
        void DebugWholeSector(int sectorIndex, int islandIndex)
        {
            Gizmos.color = _colors[islandIndex % _colors.Length];
            int2 sector2d = FlowFieldUtilities.To2D(sectorIndex, FlowFieldUtilities.SectorMatrixColAmount);
            NativeSlice<byte> costs = new NativeSlice<byte>(costsl, sectorIndex * FlowFieldUtilities.SectorTileAmount, FlowFieldUtilities.SectorTileAmount);
            for (int i = 0; i < costs.Length; i++)
            {
                byte cost = costs[i];
                if (cost == byte.MaxValue) { continue; }
                int2 local2d = FlowFieldUtilities.To2D(i, FlowFieldUtilities.SectorColAmount);
                int2 general2d = FlowFieldUtilities.GetGeneral2d(local2d, sector2d, FlowFieldUtilities.SectorColAmount, FlowFieldUtilities.FieldColAmount);
                float3 pos = new float3(general2d.x * tileSize, yOffset, general2d.y * tileSize);

                Gizmos.DrawMesh(_debugMesh, pos);
            }
        }
    }
}
