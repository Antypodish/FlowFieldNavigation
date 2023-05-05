public struct PortalNode
{
    public Portal Portal;
    public int WinPtr;
    public int PorToPorPtr;
    public int PorToPorCnt;

    public PortalNode(Portal portal, int winPtr, int porToPorPtr, int porToPorCnt)
    {
        Portal = portal;
        WinPtr = winPtr;
        PorToPorPtr = porToPorPtr;
        PorToPorCnt = porToPorCnt;
    }
}
