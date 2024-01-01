﻿using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

public struct AgentSpatialHashGrid
{
    public float BaseSpatialGridSize;
    public float FieldHorizontalSize;
    public float FieldVerticalSize;
    public NativeArray<AgentMovementData> RawAgentMovementDataArray;
    public NativeArray<UnsafeList<HashTile>> AgentHashGridArray;
    public int GetGridCount() => AgentHashGridArray.Length;
    public SpatialHashGridIterator GetIterator(float2 agentPos, float checkRange, int hashGridIndex)
    {
        if(AgentHashGridArray.Length <= hashGridIndex) { return new SpatialHashGridIterator(); }
        float tileSize = hashGridIndex * BaseSpatialGridSize + BaseSpatialGridSize;
        int2 startingTileIndex = GetStartingTileIndex(agentPos, tileSize);
        int offset = GetOffset(checkRange + tileSize / 2, tileSize);
        int2 botleft = startingTileIndex - new int2(offset, offset);
        int2 topright = startingTileIndex + new int2(offset, offset);
        botleft.x = math.select(botleft.x, 0, botleft.x < 0);
        botleft.y = math.select(botleft.y, 0, botleft.y < 0);
        int gridRowAmount = (int)math.ceil(FieldVerticalSize / tileSize);
        int gridColAmount = (int)math.ceil(FieldHorizontalSize / tileSize);
        topright.x = math.select(topright.x, gridColAmount - 1, topright.x >= gridColAmount);
        topright.y = math.select(topright.y, gridRowAmount - 1, topright.y >= gridRowAmount);
        int botleft1d = botleft.y * gridColAmount + botleft.x;
        int verticalSize = topright.y - botleft.y + 1;
        
        return new SpatialHashGridIterator(botleft1d, verticalSize, topright.x - botleft.x + 1, gridColAmount, RawAgentMovementDataArray, AgentHashGridArray[hashGridIndex]);
    }
    public int2 GetStartingTileIndex(float2 position, float tileSize) => new int2((int)math.floor(position.x / tileSize), (int)math.floor(position.y / tileSize));
    public int GetOffset(float size, float tileSize) => (int)math.ceil(size / tileSize);
}
public struct SpatialHashGridIterator
{
    int _endRowIndex;
    int _colCount;
    int _gridTotalColAmount;
    int _curRowIndex;
    NativeArray<AgentMovementData> _agentMovementDataArray;
    UnsafeList<HashTile> _hashTileArray;

    public SpatialHashGridIterator(int startRowIndex, int rowCount, int colCount, int gridTotalColCount, NativeArray<AgentMovementData> agnetMovementDataArray, UnsafeList<HashTile> hashtTileArray)
    {
        _curRowIndex = startRowIndex;
        _gridTotalColAmount = gridTotalColCount;
        _colCount = colCount;
        _endRowIndex = startRowIndex + (rowCount - 1) * gridTotalColCount;
        _agentMovementDataArray = agnetMovementDataArray;
        _hashTileArray = hashtTileArray;
    }
    public bool HasNext() => _curRowIndex <= _endRowIndex;
    public NativeSlice<AgentMovementData> GetNextRow(out int sliceStartIndex)
    {
        if(_curRowIndex > _endRowIndex) { sliceStartIndex = 0; return new NativeSlice<AgentMovementData>(); }
        HashTile startCellPointer = _hashTileArray[_curRowIndex];
        int agentStart = startCellPointer.Start;
        int agentCount = startCellPointer.Length;

        for (int i = 1; i < _colCount; i++)
        {
            HashTile newHashTile = _hashTileArray[_curRowIndex + i];
            agentCount += newHashTile.Length;
        }
        _curRowIndex += _gridTotalColAmount;

        NativeSlice<AgentMovementData> slice = new NativeSlice<AgentMovementData>(_agentMovementDataArray, agentStart, agentCount);
        sliceStartIndex = agentStart;
        return slice;
    }
}
