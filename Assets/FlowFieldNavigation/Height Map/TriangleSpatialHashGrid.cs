using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Mathematics;

public struct TriangleSpatialHashGrid
{
    public float BaseSpatialGridSize;
    public float FieldHorizontalSize;
    public float FieldVerticalSize;
    public NativeArray<int> HashedTriangleStartIndicies;
    public NativeArray<UnsafeList<HashTile>> TriangleHashGrids;
    public NativeHashMap<int, float> GridIndexToTileSize;
    public int GetGridCount() => TriangleHashGrids.Length;
    public TriangleSpatialHashGridIterator GetIterator(float2 trigBoxPos, float trigHaldMaxLength, int hashGridIndex)
    {
        if (TriangleHashGrids.Length <= hashGridIndex) { return new TriangleSpatialHashGridIterator(); }
        bool succesfull = GridIndexToTileSize.TryGetValue(hashGridIndex, out float tileSize);
        if(!succesfull) { return new TriangleSpatialHashGridIterator(); }


        int2 startingTileIndex = GetStartingTileIndex(trigBoxPos, tileSize);
        int offset = GetOffset(trigHaldMaxLength + tileSize / 2, tileSize);
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
        return new TriangleSpatialHashGridIterator(botleft1d, verticalSize, topright.x - botleft.x + 1, gridColAmount, HashedTriangleStartIndicies, TriangleHashGrids[hashGridIndex]);
    }
    int2 GetStartingTileIndex(float2 position, float tileSize) => new int2((int)math.floor(position.x / tileSize), (int)math.floor(position.y / tileSize));
    int GetOffset(float size, float tileSize) => (int)math.ceil(size / tileSize);
}
public struct TriangleSpatialHashGridIterator
{
    int _endRowIndex;
    int _colCount;
    int _gridTotalColAmount;
    int _curRowIndex;
    NativeArray<int> _hashedTriangleStartIndicies;
    UnsafeList<HashTile> _hashTileArray;

    public TriangleSpatialHashGridIterator(int startRowIndex, int rowCount, int colCount, int gridTotalColCount, NativeArray<int> hashedTriangleStartIndicies, UnsafeList<HashTile> hashtTileArray)
    {
        _curRowIndex = startRowIndex;
        _gridTotalColAmount = gridTotalColCount;
        _colCount = colCount;
        _endRowIndex = startRowIndex + (rowCount - 1) * gridTotalColCount;
        _hashedTriangleStartIndicies = hashedTriangleStartIndicies;
        _hashTileArray = hashtTileArray;
    }
    public bool HasNext() => _curRowIndex <= _endRowIndex;
    public NativeSlice<int> GetNextRow(out int sliceStartIndex)
    {
        if (_curRowIndex > _endRowIndex) { sliceStartIndex = 0; return new NativeSlice<int>(); }
        HashTile startCellPointer = _hashTileArray[_curRowIndex];
        int trigPointerStart = startCellPointer.Start;
        int trigPointerCount = startCellPointer.Length;

        for (int i = 1; i < _colCount; i++)
        {
            HashTile newHashTile = _hashTileArray[_curRowIndex + i];
            trigPointerCount += newHashTile.Length;
        }
        _curRowIndex += _gridTotalColAmount;

        NativeSlice<int> slice = new NativeSlice<int>(_hashedTriangleStartIndicies, trigPointerStart, trigPointerCount);
        sliceStartIndex = trigPointerStart;
        return slice;
    }
}