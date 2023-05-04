public struct PortalNode
{
    public Portal Portal;
    public int WinPtr;

    public PortalNode(Portal portal, int winPtr)
    {
        Portal = portal;
        WinPtr = winPtr;
    }
}
