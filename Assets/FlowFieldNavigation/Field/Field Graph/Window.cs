

namespace FlowFieldNavigation
{

    internal struct Window
    {
        internal Index2 BottomLeftBoundary;
        internal Index2 TopRightBoundary;

        internal Window(Index2 bottomLeftBoundary, Index2 topRightBoundary)
        {
            BottomLeftBoundary = bottomLeftBoundary;
            TopRightBoundary = topRightBoundary;
        }
        internal bool IsHorizontal() => (TopRightBoundary.C - BottomLeftBoundary.C) > (TopRightBoundary.R - BottomLeftBoundary.R);
    }
}