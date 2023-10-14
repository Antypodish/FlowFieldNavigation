using Unity.Mathematics;

public static class FlowFieldUtilities
{
    public static bool DebugMode;
    public static float TileSize;
    public static int SectorColAmount;
    public static int SectorRowAmount;
    public static int SectorTileAmount;
    public static int FieldColAmount;
    public static int FieldRowAmount;
    public static int FieldTileAmount;
    public static int SectorMatrixColAmount;
    public static int SectorMatrixRowAmount;
    public static int SectorMatrixTileAmount;
    public static float BaseSpatialGridSize;
    public static float MinAgentSize;
    public static float MaxAgentSize;

    public static int To1D(int2 index2, int colAmount)
    {
        return index2.y * colAmount + index2.x;
    }
    public static int2 To2D(int index, int colAmount)
    {
        return new int2(index % colAmount, index / colAmount);
    }
    public static int2 PosTo2D(float2 pos, float tileSize)
    {
        return new int2((int)math.floor(pos.x / tileSize), (int)math.floor(pos.y / tileSize));
    }
    public static float2 IndexToPos(int general1d, float tileSize, int fieldColAmount)
    {
        int2 general2d = To2D(general1d, fieldColAmount);
        return new float2(general2d.x * tileSize + tileSize / 2, general2d.y * tileSize + tileSize / 2);
    }
    public static int2 GetSectorIndex(int2 index, int sectorColAmount)
    {
        return new int2(index.x / sectorColAmount, index.y / sectorColAmount);
    }
    public static int2 GetLocalIndex(int2 index, int2 sectorStartIndex)
    {
        return index - sectorStartIndex;
    }
    public static int2 GetSectorStartIndex(int2 sectorIndex, int sectorColAmount)
    {
        return new int2(sectorIndex.x * sectorColAmount, sectorIndex.y * sectorColAmount);
    }
    public static int GetGeneral1d(int2 local2d, int2 sector2d, int sectorColAmount, int fieldColAmount)
    {
        int2 sectorStart = GetSectorStartIndex(sector2d, sectorColAmount);
        int2 general2d = local2d + sectorStart;
        int general1d = To1D(general2d, fieldColAmount);
        return general1d;
    }
}
