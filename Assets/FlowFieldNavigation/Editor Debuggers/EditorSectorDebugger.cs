#if (UNITY_EDITOR) 

using Unity.Collections;
using System.Collections.Generic;
using UnityEngine;
internal class EditorSectorDebugger
{
    PathfindingManager _pathfindingManager;
    SectorDebugMeshBuilder _sectorDebugMeshContainer;
    internal EditorSectorDebugger(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
        _sectorDebugMeshContainer = new SectorDebugMeshBuilder(pathfindingManager);
    }

    internal void DebugSectors()
    {
        List<Mesh> meshes = _sectorDebugMeshContainer.GetDebugMesh();
        Gizmos.color = Color.black;
        for(int i = 0; i < meshes.Count; i++)
        {
            Gizmos.DrawMesh(meshes[i]);
        }
    }
}
#endif
