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
    public bool IsOnCorner(int colAmount, int rowAmount) => (IsOnTop(rowAmount) && IsOnRight(colAmount)) || (IsOnTop(rowAmount) && IsOnLeft()) || (IsOnBottom() && IsOnRight(colAmount)) || (IsOnBottom() && IsOnLeft());
    public bool IsOnEdge(int colAmount, int rowAmount) => IsOnTop(rowAmount) || IsOnBottom() || IsOnRight(colAmount) || IsOnLeft();
    public bool IsOnTop(int rowAmount) => StartIndex.R + Size >= rowAmount;
    public bool IsOnBottom() => StartIndex.R == 0;
    public bool IsOnRight(int colAmount) => StartIndex.C + Size >= colAmount;
    public bool IsOnLeft() => StartIndex.C == 0;
    public bool IsOnTopLeft(int rowAmount) => (StartIndex.R + Size >= rowAmount) && StartIndex.C == 0;
    public bool IsOnTopRight(int rowAmount, int colAmount) => (StartIndex.R + Size >= rowAmount) && StartIndex.C + Size >= colAmount;
    public bool IsOnBottomLeft() => (StartIndex.R == 0) && StartIndex.C == 0;
    public bool IsOnBottomRight(int colAmount) => (StartIndex.R == 0) && StartIndex.C + Size >= colAmount;
    public static bool operator ==(Sector sector1, Sector sector2)
    {
        return sector1.StartIndex == sector2.StartIndex;
    }
    public static bool operator !=(Sector sector1, Sector sector2)
    {
        return sector1.StartIndex != sector2.StartIndex;
    }
}