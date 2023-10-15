using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

public class EditorSpatialHashGridDebugger
{
    PathfindingManager _pathfindingManager;

    Color[] _colors;
    public EditorSpatialHashGridDebugger(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
        _colors = new Color[]
        {
            Color.black,
            Color.white,
            Color.blue,
            Color.green,
            Color.gray,
            Color.red,
            Color.cyan,
            Color.yellow,
            Color.magenta,
        };
    }

    public void Debug(int gridIndex)
    {
        NativeArray<UnsafeList<HashTile>> hashGridArray = _pathfindingManager.GetSpatialHashGridArray();
        if(gridIndex >= hashGridArray.Length) { return; }
        NativeArray<int> normalToHashed = _pathfindingManager.GetNormalToHashed();
        NativeArray<AgentMovementData> movData = _pathfindingManager.GetAgentMovementData();
        NativeArray<AgentData> agentData = _pathfindingManager.AgentDataContainer.AgentDataList;

        UnsafeList<HashTile> pickedGrid = hashGridArray[gridIndex];
        AgentSpatialGridUtils gridUtils = new AgentSpatialGridUtils(0);

        DrawTileBorders();

        for(int i = 0; i < pickedGrid.Length; i++)
        {
            Gizmos.color = _colors[i % _colors.Length];
            HashTile tile = pickedGrid[i];
            for(int j = tile.Start; j < tile.Start + tile.Length; j++)
            {
                Vector3 position = movData[j].Position;
                Gizmos.DrawSphere(position, 0.5f);
            } 
        }

        void DrawTileBorders()
        {
            Gizmos.color = Color.black;
            float tileSize = gridUtils.GetTileSize(gridIndex);
            int colAmount = gridUtils.GetColAmount(gridIndex);
            int rowAmount = gridUtils.GetRowAmount(gridIndex);

            float maxZ = rowAmount * tileSize;
            float maxX = colAmount * tileSize;
            float yOffset = 0.2f;
            for(int i = 0; i < colAmount; i++)
            {
                Vector3 start = new Vector3(i * tileSize, yOffset, 0f);
                Vector3 end = new Vector3(start.x, yOffset, maxZ);
                Gizmos.DrawLine(start, end);
            }
            for (int i = 0; i < rowAmount; i++)
            {
                Vector3 start = new Vector3(0f, yOffset, tileSize * i);
                Vector3 end = new Vector3(maxX, yOffset, start.z);
                Gizmos.DrawLine(start, end);
            }
        }
    }
}
