using Unity.Collections;

public struct WindowArray
{
    public NativeArray<WindowNode> Nodes;
    public NativeArray<int> SecPtrs;

    public WindowArray(int windowNodeAmount, int sectorPointerAmount)
    {
        Nodes = new NativeArray<WindowNode>(windowNodeAmount, Allocator.Persistent);
        SecPtrs = new NativeArray<int>(sectorPointerAmount, Allocator.Persistent);
    }
    public void DisposeNatives()
    {
        Nodes.Dispose();
        SecPtrs.Dispose();
    }
}
