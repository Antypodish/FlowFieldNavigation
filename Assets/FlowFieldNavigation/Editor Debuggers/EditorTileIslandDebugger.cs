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

internal class EditorTileIslandDebugger
{
    TileIslandDebugMeshBuilder _dubugMeshBuilder;
    PathfindingManager _pathfindingManager;
    Color[] _colors;

    internal EditorTileIslandDebugger(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
        _dubugMeshBuilder = new TileIslandDebugMeshBuilder(pathfindingManager);
        _colors = new Color[]{
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
            new Color(0,0,0),
        };
    }
    internal void DebugTileIslands(int offset)
    {
        _dubugMeshBuilder.GetTileIslandDebugMesh(offset, out List<Mesh> debugMeshes, out List<int> debugMeshColorIndicies);
        for(int i = 0; i < debugMeshes.Count; i++)
        {
            Color color = _colors[debugMeshColorIndicies[i] % _colors.Length];
            color.a = 0.25f;
            Gizmos.color = color;
            Gizmos.DrawMesh(debugMeshes[i]);
        }
    }
}
