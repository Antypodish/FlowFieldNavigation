public struct Window
{
    public Index2 BottomLeftBoundary;
    public Index2 TopRightBoundary;

    public Window(Index2 bottomLeftBoundary, Index2 topRightBoundary)
    {
        BottomLeftBoundary = bottomLeftBoundary;
        TopRightBoundary = topRightBoundary;
    }
    public bool IsHorizontal() => (TopRightBoundary.C - BottomLeftBoundary.C) > (TopRightBoundary.R - BottomLeftBoundary.R);
}