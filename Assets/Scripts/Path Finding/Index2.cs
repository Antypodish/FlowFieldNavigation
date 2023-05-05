public struct Index2
{
    public int R;
    public int C;

    public Index2(int row, int column)
    {
        R = row;
        C = column;
    }
    public static bool operator == (Index2 index1, Index2 index2)
    {
        return index1.R == index2.R && index1.C == index2.C;
    }
    public static bool operator !=(Index2 index1, Index2 index2)
    {
        return index1.R != index2.R || index1.C != index2.C;
    }
    public static int ToIndex(Index2 index2, int rowAmount)
    {
        return index2.R * rowAmount + index2.C;
    }
    public static Index2 ToIndex2(int index, int rowAmount)
    {
        return new Index2(index / rowAmount, index % rowAmount);
    }
    public override string ToString()
    {
        return "["+R+", "+C+"]";
    }
}
