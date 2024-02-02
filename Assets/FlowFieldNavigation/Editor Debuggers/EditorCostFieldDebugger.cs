#if (UNITY_EDITOR) 

using UnityEngine;
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

internal class EditorCostFieldDebugger
{
    CostFieldDebugMeshBuilder _costFieldDebugMeshContainer;

    internal EditorCostFieldDebugger(PathfindingManager pathfindingManager)
    {
        _costFieldDebugMeshContainer = new CostFieldDebugMeshBuilder(pathfindingManager);
    }
    internal void DebugCostFieldWithMesh(int offset)
    {
        List<Mesh> debugMesh = _costFieldDebugMeshContainer.GetDebugMesh(offset);
        Gizmos.color = new Color(1,0,0,0.5f);
        for(int i = 0; i < debugMesh.Count; i++)
        {
            Gizmos.DrawMesh(debugMesh[i]);
        }
    }
}
#endif