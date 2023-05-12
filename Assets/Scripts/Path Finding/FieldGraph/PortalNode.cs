using UnityEditor.Experimental.GraphView;

public struct PortalNode
{
    public Portal Portal1;
    public Portal Portal2;
    public int WinPtr;

    public PortalNode(Portal portal1, Portal portal2, int winPtr)
    {
        Portal1 = portal1;
        Portal2 = portal2;
        WinPtr = winPtr;
    }
    public PortalNode(Portal portal1, int winPtr)
    {
        Portal1 = portal1;
        Portal2 = new Portal();
        WinPtr = winPtr;
    }
}