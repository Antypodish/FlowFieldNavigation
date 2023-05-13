#if (UNITY_EDITOR) 

using Unity.Collections;
using UnityEngine;

public class TilePositionDebugger
{
    PathfindingManager _pathfindingManager;

    public TilePositionDebugger(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
    }

    public void DebugTilePositions()
    {
        NativeArray<Vector3> tilePositions = _pathfindingManager.TilePositions;
        Gizmos.color = Color.cyan;
        for (int i = 0; i < tilePositions.Length; i++)
        {
            Vector3 pos = tilePositions[i];
            Gizmos.DrawCube(pos, Vector3.one / 4);
        }
    }
}

#endif