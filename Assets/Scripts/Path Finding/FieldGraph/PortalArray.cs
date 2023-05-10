using Unity.Collections;

public struct PortalArray
{
    public NativeArray<PortalNode> Nodes;
    public NativeArray<PortalToPortal> PorPtrs;

    public PortalArray(int portalNodeAmount, int porToPorPtrAmount)
    {
        Nodes = new NativeArray<PortalNode>(portalNodeAmount, Allocator.Persistent);
        PorPtrs = new NativeArray<PortalToPortal>(porToPorPtrAmount, Allocator.Persistent);
    }
    public void DisposeNatives()
    {
        Nodes.Dispose();
        PorPtrs.Dispose();
    }
}
