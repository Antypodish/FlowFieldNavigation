using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;
using System.Diagnostics;
using System.Collections.Generic;
using UnityEditor;
internal class EditorHeightMeshDebugger
{
    FlowFieldNavigationManager _navigationManager;
    HeightDebugMeshBuilder _heightDebugMeshBuilder;
    GenericDebugTileMeshBuilder _genericTileMeshBuilder;
    internal EditorHeightMeshDebugger(FlowFieldNavigationManager navigationManager)
    {
        _navigationManager = navigationManager;
        _heightDebugMeshBuilder = new HeightDebugMeshBuilder(navigationManager);
        _genericTileMeshBuilder = new GenericDebugTileMeshBuilder(navigationManager);
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
        TriangleSpatialHashGrid triangleHashGrid = _navigationManager.FieldDataContainer.HeightMeshGenerator.GetTriangleSpatialHashGrid();
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
