using Unity.Collections;

public struct WindowNode
{
    public Window Window;
    public int WinToSecPtr;
    public int WinToSecCnt;
    public int PorPtr;
    public int PorCnt;

    public WindowNode(Window window, int winToSecCnt, int winToSecPtr, int porPtr, int porCnt, NativeArray<byte> costs)
    {
        Window = window;
        WinToSecCnt = winToSecCnt;
        WinToSecPtr = winToSecPtr;
        PorPtr = porPtr;
        PorCnt = porCnt;
    }
    
}