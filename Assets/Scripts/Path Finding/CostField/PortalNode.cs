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
public struct Portal
{
    public Index2 Index1;
    public Index2 Index2;

    public Portal(Index2 index1, Index2 index2)
    {
        Index1 = index1;
        Index2 = index2;
    }
}
public struct PortalToPortal
{
    public float Distance;
    public int Index;

    public PortalToPortal(float distance, int index)
    {
        Distance = distance;
        Index = index;
    }
}
