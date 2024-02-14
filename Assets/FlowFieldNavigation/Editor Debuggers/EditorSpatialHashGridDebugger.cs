using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using System.Collections.Generic;

internal class EditorSpatialHashGridDebugger
{
    FlowFieldNavigationManager _navigationManager;
    GenericDebugTileMeshBuilder _genericDebugTileMeshBuilder;
    Color[] _colors;
    internal EditorSpatialHashGridDebugger(FlowFieldNavigationManager navigationManager)
    {
        _navigationManager = navigationManager;
        _genericDebugTileMeshBuilder = new GenericDebugTileMeshBuilder(navigationManager);
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

    internal void DebugAgent(FlowFieldAgent agent, int gridIndex)
    {
        NativeArray<UnsafeList<HashTile>> hashGridArray = _navigationManager.GetSpatialHashGridArray();
        if (gridIndex >= hashGridArray.Length || gridIndex < 0) { return; }
        
        NativeArray<int> normalToHashed = _navigationManager.GetNormalToHashed();
        NativeArray<AgentMovementData> movData = _navigationManager.GetAgentMovementData();
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
        NativeArray<UnsafeList<HashTile>> hashGridArray = _navigationManager.GetSpatialHashGridArray();
        if (gridIndex >= hashGridArray.Length || gridIndex < 0) { return; }
        AgentSpatialHashGrid grid = new AgentSpatialHashGrid()
        {
            BaseSpatialGridSize = FlowFieldUtilities.BaseAgentSpatialGridSize,
            FieldGridStartPosition = FlowFieldUtilities.FieldGridStartPosition,
            FieldVerticalSize = FlowFieldUtilities.FieldRowAmount * FlowFieldUtilities.TileSize,
            FieldHorizontalSize = FlowFieldUtilities.FieldColAmount * FlowFieldUtilities.TileSize,
            AgentHashGridArray = hashGridArray,
        };

        Gizmos.color = Color.black;
        float tileSize = grid.GetTileSize(gridIndex);
        int colAmount = grid.GetColAmount(gridIndex);
        int rowAmount = grid.GetRowAmount(gridIndex);

        List<Mesh> meshes = _genericDebugTileMeshBuilder.GetDebugMesh(colAmount, rowAmount, tileSize, FlowFieldUtilities.FieldGridStartPosition);
        for (int i = 0; i < meshes.Count; i++)
        {
            Gizmos.DrawMesh(meshes[i]);
        }
    }
    internal void Debug(int gridIndex)
    {
        NativeArray<UnsafeList<HashTile>> hashGridArray = _navigationManager.GetSpatialHashGridArray();
        if(gridIndex >= hashGridArray.Length || gridIndex < 0) { return; }
        NativeArray<AgentMovementData> movData = _navigationManager.GetAgentMovementData();
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
