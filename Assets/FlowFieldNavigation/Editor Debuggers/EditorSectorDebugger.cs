#if (UNITY_EDITOR) 

using Unity.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FlowFieldNavigation
{
    internal class EditorSectorDebugger
    {
        FlowFieldNavigationManager _navigationManager;
        SectorDebugMeshBuilder _sectorDebugMeshContainer;
        internal EditorSectorDebugger(FlowFieldNavigationManager navigationManager)
        {
            _navigationManager = navigationManager;
            _sectorDebugMeshContainer = new SectorDebugMeshBuilder(navigationManager);
        }

        internal void DebugSectors()
        {
            List<Mesh> meshes = _sectorDebugMeshContainer.GetDebugMesh();
            Gizmos.color = Color.black;
            for (int i = 0; i < meshes.Count; i++)
            {
                Gizmos.DrawMesh(meshes[i]);
            }
        }
    }

}
#endif
