internal struct Sector
{
    internal Index2 StartIndex;
    internal int Size;

    internal Sector(Index2 startIndex, int size)
    {
        StartIndex = startIndex;
        Size = size;
    }
    internal bool ContainsIndex(Index2 index)
    {
        if (index.R < StartIndex.R) { return false; }
        if (index.C < StartIndex.C) { return false; }
        if (index.R >= StartIndex.R + Size) { return false; }
        if (index.C >= StartIndex.C + Size) { return false; }
        return true;
    }
    internal bool IsOnCorner(int colAmount, int rowAmount) => (IsOnTop(rowAmount) && IsOnRight(colAmount)) || (IsOnTop(rowAmount) && IsOnLeft()) || (IsOnBottom() && IsOnRight(colAmount)) || (IsOnBottom() && IsOnLeft());
    internal bool IsOnEdge(int colAmount, int rowAmount) => IsOnTop(rowAmount) || IsOnBottom() || IsOnRight(colAmount) || IsOnLeft();
    internal bool IsOnTop(int rowAmount) => StartIndex.R + Size >= rowAmount;
    internal bool IsOnBottom() => StartIndex.R == 0;
    internal bool IsOnRight(int colAmount) => StartIndex.C + Size >= colAmount;
    internal bool IsOnLeft() => StartIndex.C == 0;
}