using UnityEngine;
using System.Collections.Generic;
using System.Diagnostics;
internal class EditorTileDebugger
{
    TileDebugMeshBuilder _tileDebugMeshContainer;
    internal EditorTileDebugger(PathfindingManager pathfindingManager)
    {
        _tileDebugMeshContainer = new TileDebugMeshBuilder(pathfindingManager);
    }
    public void Debug()
    {
        List<Mesh> debugMeshes = _tileDebugMeshContainer.GetDebugMesh();
        Gizmos.color = Color.black;
        for(int i = 0; i < debugMeshes.Count; i++)
        {
            Gizmos.DrawMesh(debugMeshes[i]);
        }
    }
}
