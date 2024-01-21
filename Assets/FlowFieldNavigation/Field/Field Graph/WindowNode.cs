using Unity.Collections;

internal struct WindowNode
{
    internal Window Window;
    internal int WinToSecPtr;
    internal int WinToSecCnt;
    internal int PorPtr;
    internal int PorCnt;

    internal WindowNode(Window window, int winToSecCnt, int winToSecPtr, int porPtr, int porCnt)
    {
        Window = window;
        WinToSecCnt = winToSecCnt;
        WinToSecPtr = winToSecPtr;
        PorPtr = porPtr;
        PorCnt = porCnt;
    }
    
}