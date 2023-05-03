using Unity.Collections;

public struct WindowNode
{
    public Window Window;
    public int WinToSecPtr;
    public int WinToSecCnt;
    public int PorPtr;
    public int PorCnt;

    public WindowNode(Window window, int winToSecCnt, int winToSecPtr, int porPtr, int tileAmount, NativeArray<byte> costs)
    {
        Window = window;
        WinToSecCnt = winToSecCnt;
        WinToSecPtr = winToSecPtr;
        PorPtr = porPtr;
        PorCnt = 0;
        SetPortalCount(costs, tileAmount);
    }
    public void SetPortalCount(NativeArray<byte> costs, int tileAmount)
    {
        if (Window.IsHorizontal())  //if horizontal
        {
            int portalAmount = 0;
            bool wasUnwalkableFlag = true;
            int startCol = Window.BottomLeftBoundary.C;
            int lastCol = Window.TopRightBoundary.C;
            int row1 = Window.BottomLeftBoundary.R;
            int row2 = Window.TopRightBoundary.R;
            for (int i = startCol; i <= lastCol; i++)
            {
                int costIndex1 = row1 * tileAmount + i;
                int costIndex2 = row2 * tileAmount + i;
                if (costs[costIndex1] == byte.MaxValue) { wasUnwalkableFlag = true; }
                else if (costs[costIndex2] == byte.MaxValue) { wasUnwalkableFlag = true; }
                else if (wasUnwalkableFlag) { portalAmount++; wasUnwalkableFlag = false; }
            }
            PorCnt = portalAmount;
        }
        else //if vertical
        {
            int portalAmount = 0;
            bool wasUnwalkableFlag = true;
            int startRow = Window.BottomLeftBoundary.R;
            int lastRow = Window.TopRightBoundary.R;
            int col1 = Window.BottomLeftBoundary.C;
            int col2 = Window.TopRightBoundary.C;
            for (int i = startRow; i <= lastRow; i++)
            {
                int costIndex1 = i * tileAmount + col1;
                int costIndex2 = i * tileAmount + col2;
                if (costs[costIndex1] == byte.MaxValue) { wasUnwalkableFlag = true; }
                else if (costs[costIndex2] == byte.MaxValue) { wasUnwalkableFlag = true; }
                else if (wasUnwalkableFlag) { portalAmount++; wasUnwalkableFlag = false; }
            }
            PorCnt = portalAmount;
        }

    }
}
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