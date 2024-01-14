public struct BoundaryData
{
    public Index2 BottomLeft;
    public Index2 UpperRight;

    public BoundaryData(Index2 bound1, Index2 bound2)
    {
        int lowerRow = bound1.R < bound2.R ? bound1.R : bound2.R;
        int upperRow = bound1.R > bound2.R ? bound1.R : bound2.R;
        int leftmostCol = bound1.C < bound2.C ? bound1.C : bound2.C;
        int rightmostCol = bound1.C > bound2.C ? bound1.C : bound2.C;

        BottomLeft = new Index2(lowerRow, leftmostCol);
        UpperRight = new Index2(upperRow, rightmostCol);
    }
}
