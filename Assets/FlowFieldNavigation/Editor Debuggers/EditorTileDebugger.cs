using UnityEngine;
using System.Collections.Generic;
using System.Diagnostics;

namespace FlowFieldNavigation
{
    internal class EditorTileDebugger
    {
        TileDebugMeshBuilder _tileDebugMeshContainer;
        internal EditorTileDebugger(FlowFieldNavigationManager navigationManager)
        {
            _tileDebugMeshContainer = new TileDebugMeshBuilder(navigationManager);
        }
        public void Debug()
        {
            List<Mesh> debugMeshes = _tileDebugMeshContainer.GetDebugMesh();
            Gizmos.color = Color.black;
            for (int i = 0; i < debugMeshes.Count; i++)
            {
                Gizmos.DrawMesh(debugMeshes[i]);
            }
        }
    }


}