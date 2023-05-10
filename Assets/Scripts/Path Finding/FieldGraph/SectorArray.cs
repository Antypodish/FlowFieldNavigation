using Unity.Collections;

public struct SectorArray
{
    public NativeArray<SectorNode> Nodes;
    public NativeArray<int> WinPtrs;

    public SectorArray(int totalSectorAmount, int secToWinPtrsAmount)
    {
        Nodes = new NativeArray<SectorNode>(totalSectorAmount, Allocator.Persistent);
        WinPtrs = new NativeArray<int>(secToWinPtrsAmount, Allocator.Persistent);
    }
    public void DisposeNatives()
    {
        Nodes.Dispose();
        WinPtrs.Dispose();
    }
    public NativeArray<int> GetPortalIndicies(SectorNode sectorNode, NativeArray<WindowNode> windowNodes)
    {
        NativeArray<int> portalIndicies;
        int secToWinCnt = sectorNode.SecToWinCnt;
        int secToWinPtr = sectorNode.SecToWinPtr;

        //determine portal count
        int portalIndexCount = 0;
        for (int i = 0; i < secToWinCnt; i++)
        {
            portalIndexCount += windowNodes[WinPtrs[secToWinPtr + i]].PorCnt;
        }
        portalIndicies = new NativeArray<int>(portalIndexCount, Allocator.Temp);

        //get portals
        int portalIndiciesIterable = 0;
        for (int i = 0; i < secToWinCnt; i++)
        {
            int windowPorPtr = windowNodes[WinPtrs[secToWinPtr + i]].PorPtr;
            int windowPorCnt = windowNodes[WinPtrs[secToWinPtr + i]].PorCnt;
            for (int j = 0; j < windowPorCnt; j++)
            {
                portalIndicies[portalIndiciesIterable++] = windowPorPtr + j;
            }
        }
        return portalIndicies;
    }
}
