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
}
