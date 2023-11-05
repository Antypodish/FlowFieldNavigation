using System;
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
    public static float2 IndexToPos(int2 general2d, float tileSize)
    {
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
    public static int2 GetGeneral2d(int2 local2d, int2 sector2d, int sectorColAmount, int fieldColAmount)
    {
        int2 sectorStart = GetSectorStartIndex(sector2d, sectorColAmount);
        int2 general2d = local2d + sectorStart;
        return general2d;
    }
    public static int2 GetLocal2dInSector(PortalNode portalNode, int sectorIndex, int sectorMatrixColAmount, int sectorColAmount)
    { 
        int2 p12d = new int2(portalNode.Portal1.Index.C, portalNode.Portal1.Index.R);
        int2 p22d = new int2(portalNode.Portal2.Index.C, portalNode.Portal2.Index.R);
        int2 sector2d = new int2(sectorIndex % sectorMatrixColAmount, sectorIndex / sectorMatrixColAmount);

        int2 p1Secpr2d = p12d / sectorColAmount;
        int2 p2Secpr2d = p22d / sectorColAmount;

        int2 picked2d = math.select(p22d, p12d, sector2d.Equals(p1Secpr2d));
        int2 sectorStart = new int2(sector2d.x * sectorColAmount, sector2d.y * sectorColAmount);

        return picked2d - sectorStart;
    }
    public static int GetLocal1dInSector(PortalNode portalNode, int sectorIndex, int sectorMatrixColAmount, int sectorColAmount)
    {
        int2 p12d = new int2(portalNode.Portal1.Index.C, portalNode.Portal1.Index.R);//(149,68)
        int2 p22d = new int2(portalNode.Portal2.Index.C, portalNode.Portal2.Index.R);//(150,68)
        int2 sector2d = new int2(sectorIndex % sectorMatrixColAmount, sectorIndex / sectorMatrixColAmount);//(15,7)

        int2 p1Secpr2d = p12d / sectorColAmount;//(14,6)

        int2 picked2d = math.select(p22d, p12d, sector2d.Equals(p1Secpr2d));//(150,68)
        int2 sectorStart = new int2(sector2d.x * sectorColAmount, sector2d.y * sectorColAmount);//(150,70)
        int2 local2d = picked2d - sectorStart;//(0,-2)

        return local2d.y * sectorColAmount + local2d.x;//-20
    }
}
