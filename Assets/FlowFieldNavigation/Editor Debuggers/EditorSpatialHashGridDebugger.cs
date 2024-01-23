using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
internal class EditorSpatialHashGridDebugger
{
    PathfindingManager _pathfindingManager;

    Color[] _colors;
    internal EditorSpatialHashGridDebugger(PathfindingManager pathfindingManager)
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

    internal void DebugAgent(FlowFieldAgent agent, int gridIndex, float checkRange)
    {
        NativeArray<UnsafeList<HashTile>> hashGridArray = _pathfindingManager.GetSpatialHashGridArray();
        if (gridIndex >= hashGridArray.Length || gridIndex < 0) { return; }
        
        NativeArray<int> normalToHashed = _pathfindingManager.GetNormalToHashed();
        NativeArray<AgentMovementData> movData = _pathfindingManager.GetAgentMovementData();
        AgentSpatialHashGrid spatialHashGrid = new AgentSpatialHashGrid()
        {
            BaseSpatialGridSize = FlowFieldUtilities.BaseAgentSpatialGridSize,
            FieldHorizontalSize = FlowFieldUtilities.TileSize * FlowFieldUtilities.FieldColAmount,
            FieldVerticalSize = FlowFieldUtilities.TileSize * FlowFieldUtilities.FieldRowAmount,
            RawAgentMovementDataArray = movData,
            AgentHashGridArray = hashGridArray,
            FieldGridStartPosition = FlowFieldUtilities.FieldGridStartPosition,
        };

        int agentIndex = agent.AgentDataIndex;
        int hashedIndex = normalToHashed[agentIndex];
        AgentMovementData agentMovData = movData[hashedIndex];
        float2 agentPos = new float2(agentMovData.Position.x, agentMovData.Position.z);
        SpatialHashGridIterator gridIterator = spatialHashGrid.GetIterator(agentPos, agentMovData.Radius, gridIndex);
        Gizmos.color = Color.black;
        Gizmos.DrawWireSphere(agentMovData.Position, agentMovData.Radius);
        Gizmos.color = Color.red;
        while (gridIterator.HasNext())
        {
            NativeSlice<AgentMovementData> curChunk = gridIterator.GetNextRow(out int sliceStartIndex);
            for(int i = 0; i < curChunk.Length; i++)
            {
                Vector3 targetPos = curChunk[i].Position;
                Gizmos.DrawSphere(targetPos, 0.2f);
                Gizmos.DrawLine(agentMovData.Position, targetPos);
            }
        }
    }
    internal void DebugBorders(int gridIndex)
    {
        NativeArray<UnsafeList<HashTile>> hashGridArray = _pathfindingManager.GetSpatialHashGridArray();
        if (gridIndex >= hashGridArray.Length || gridIndex < 0) { return; }
        AgentSpatialHashGrid grid = new AgentSpatialHashGrid()
        {
            BaseSpatialGridSize = FlowFieldUtilities.BaseAgentSpatialGridSize,
            FieldGridStartPosition = FlowFieldUtilities.FieldGridStartPosition,
            FieldVerticalSize = FlowFieldUtilities.FieldRowAmount * FlowFieldUtilities.TileSize,
            FieldHorizontalSize = FlowFieldUtilities.FieldColAmount * FlowFieldUtilities.TileSize,
            AgentHashGridArray = hashGridArray,
        };

        DrawTileBorders();
        void DrawTileBorders()
        {
            Gizmos.color = Color.black;
            float tileSize = grid.GetTileSize(gridIndex);
            int colAmount = grid.GetColAmount(gridIndex);
            int rowAmount = grid.GetRowAmount(gridIndex);
            float2 maxPos = FlowFieldUtilities.GetGridMaxPosExcluding(rowAmount, colAmount, tileSize, FlowFieldUtilities.FieldGridStartPosition);
            float maxZ = maxPos.y;
            float maxX = maxPos.x;
            float yOffset = 0.2f;
            for (int i = 0; i < colAmount; i++)
            {
                float2 start2 = new float2(i * tileSize, 0f);
                float2 end2 = new float2(start2.x, maxZ);
                start2 += FlowFieldUtilities.HeightMeshStartPosition;
                end2 += FlowFieldUtilities.HeightMeshStartPosition;
                Vector3 start = new Vector3(start2.x, yOffset, start2.y);
                Vector3 end = new Vector3(end2.x, yOffset, end2.y);
                Gizmos.DrawLine(start, end);
            }
            for (int i = 0; i < rowAmount; i++)
            {
                float2 start2 = new float2(0f, tileSize * i);
                float2 end2 = new float2(maxX, start2.y);
                start2 += FlowFieldUtilities.HeightMeshStartPosition;
                end2 += FlowFieldUtilities.HeightMeshStartPosition;
                Vector3 start = new Vector3(start2.x, yOffset, start2.y);
                Vector3 end = new Vector3(end2.x, yOffset, end2.y);
                Gizmos.DrawLine(start, end);
            }
        }

    }
    internal void Debug(int gridIndex)
    {
        NativeArray<UnsafeList<HashTile>> hashGridArray = _pathfindingManager.GetSpatialHashGridArray();
        if(gridIndex >= hashGridArray.Length || gridIndex < 0) { return; }
        NativeArray<AgentMovementData> movData = _pathfindingManager.GetAgentMovementData();
        UnsafeList<HashTile> pickedGrid = hashGridArray[gridIndex];

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
    }
}
