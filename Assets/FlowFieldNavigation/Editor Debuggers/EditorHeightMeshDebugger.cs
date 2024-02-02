using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;
using System.Diagnostics;
using System.Collections.Generic;
internal class EditorHeightMeshDebugger
{
    PathfindingManager _pathfindingManager;
    HeightDebugMeshBuilder _heightDebugMeshBuilder;
    GenericDebugTileMeshBuilder _genericTileMeshBuilder;
    internal EditorHeightMeshDebugger(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
        _heightDebugMeshBuilder = new HeightDebugMeshBuilder(pathfindingManager);
        _genericTileMeshBuilder = new GenericDebugTileMeshBuilder(pathfindingManager);
    }

    internal void DebugHeightMapMesh()
    {
        List<Mesh> meshes = _heightDebugMeshBuilder.GetHeightDebugMeshes();
        Gizmos.color = Color.black;
        for(int i = 0; i <meshes.Count; i++)
        {
            Gizmos.DrawWireMesh(meshes[i]);
        }
    }
    internal void DebugBorders(int gridIndex)
    {
        Gizmos.color = Color.black;
        TriangleSpatialHashGrid triangleHashGrid = _pathfindingManager.FieldDataContainer.HeightMeshGenerator.GetTriangleSpatialHashGrid();
        if (gridIndex < 0 || gridIndex > triangleHashGrid.GetGridCount()) { return; }
        float tileSize = triangleHashGrid.GetGridTileSize(gridIndex);
        int colAmount = triangleHashGrid.GetGridColAmount(gridIndex);
        int rowAmount = triangleHashGrid.GetGridRowAmount(gridIndex);

        List<Mesh> meshes = _genericTileMeshBuilder.GetDebugMesh(colAmount, rowAmount, tileSize, FlowFieldUtilities.HeightMeshStartPosition);
        for(int i = 0; i < meshes.Count; i++)
        {
            Gizmos.DrawMesh(meshes[i]);
        }
    }
}
