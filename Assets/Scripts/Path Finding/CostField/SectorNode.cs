using Unity.Collections;

public struct SectorNode
{
    public Sector Sector;
    public int SecToWinPtr;
    public int SecToWinCnt;

    public SectorNode(Sector sector, int secToWinCnt, int secToWinPtr)
    {
        Sector = sector;
        SecToWinCnt = secToWinCnt;
        SecToWinPtr = secToWinPtr;
    }
}
public struct Sector
{
    public Index2 StartIndex;
    public int Size;

    public Sector(Index2 startIndex, int size)
    {
        StartIndex = startIndex;
        Size = size;
    }
    public bool ContainsIndex(Index2 index)
    {
        if (index.R < StartIndex.R) { return false; }
        if (index.C < StartIndex.C) { return false; }
        if (index.R >= StartIndex.R + Size) { return false; }
        if (index.C >= StartIndex.C + Size) { return false; }
        return true;
    }
    public bool IsOnCorner(int matrixSize) => (IsOnTop(matrixSize) && IsOnRight(matrixSize)) || (IsOnTop(matrixSize) && IsOnLeft()) || (IsOnBottom() && IsOnRight(matrixSize)) || (IsOnBottom() && IsOnLeft());
    public bool IsOnEdge(int matrixSize) => IsOnTop(matrixSize) || IsOnBottom() || IsOnRight(matrixSize) || IsOnLeft();
    public bool IsOnTop(int matrixSize) => StartIndex.R + Size >= matrixSize;
    public bool IsOnBottom() => StartIndex.R == 0;
    public bool IsOnRight(int matrixSize) => StartIndex.C + Size >= matrixSize;
    public bool IsOnLeft() => StartIndex.C == 0;
    public bool IsOnTopLeft(int matrixSize) => (StartIndex.R + Size >= matrixSize) && StartIndex.C == 0;
    public bool IsOnTopRight(int matrixSize) => (StartIndex.R + Size >= matrixSize) && StartIndex.C + Size >= matrixSize;
    public bool IsOnBottomLeft(int matrixSize) => (StartIndex.R == 0) && StartIndex.C == 0;
    public bool IsOnBottomRight(int matrixSize) => (StartIndex.R == 0) && StartIndex.C + Size >= matrixSize;
    public static bool operator ==(Sector sector1, Sector sector2)
    {
        return sector1.StartIndex == sector2.StartIndex;
    }
    public static bool operator !=(Sector sector1, Sector sector2)
    {
        return sector1.StartIndex != sector2.StartIndex;
    }
}


