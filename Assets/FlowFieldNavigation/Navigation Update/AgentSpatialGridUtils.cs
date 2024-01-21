using Unity.Burst;
using Unity.Mathematics;

[BurstCompile]
internal struct AgentSpatialGridUtils
{
    internal float BaseSpatialGridSize;
    internal float FieldHorizontalSize;
    internal float FieldVerticalSize;

    internal AgentSpatialGridUtils(byte placeholderParameter)
    {
        BaseSpatialGridSize = FlowFieldUtilities.BaseAgentSpatialGridSize;
        FieldHorizontalSize = FlowFieldUtilities.TileSize * FlowFieldUtilities.FieldColAmount;
        FieldVerticalSize = FlowFieldUtilities.TileSize * FlowFieldUtilities.FieldRowAmount;
    }
    internal GridTravesalData GetGridTraversalData(float2 agentPos, float checkRange, int hashGridIndex)
    {
        float tileSize = hashGridIndex * BaseSpatialGridSize + BaseSpatialGridSize;
        int2 startingTileIndex = GetStartingTileIndex(agentPos, tileSize);
        int offset = GetOffset(checkRange * 2, tileSize);
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

        return new GridTravesalData()
        {
            horizontalSize = topright.x - botleft.x + 1,
            botLeft = botleft1d,
            gridColAmount = gridColAmount,
            topLeft = botleft1d + (verticalSize * gridColAmount) - 1,
        };
    }
    internal int2 GetStartingTileIndex(float2 position, float tileSize) => new int2((int)math.floor(position.x / tileSize), (int)math.floor(position.y / tileSize));
    internal int GetOffset(float size, float tileSize) => (int)math.ceil(size / tileSize);
    internal float GetTileSize(int hashGridIndex)
    {
        return hashGridIndex * BaseSpatialGridSize + BaseSpatialGridSize;
    }
    internal int GetColAmount(int hashGridIndex)
    {
        return (int)math.ceil(FieldHorizontalSize / GetTileSize(hashGridIndex));
    }
    internal int GetRowAmount(int hashGridIndex)
    {
        return (int)math.ceil(FieldVerticalSize / GetTileSize(hashGridIndex));
    }
}
internal struct GridTravesalData
{
    internal int botLeft;
    internal int topLeft;
    internal int horizontalSize;
    internal int gridColAmount;
}