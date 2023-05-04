using Unity.Collections;

public struct SectorGraph
{
    public SectorNodes SectorNodes;
    public WindowNodes WindowNodes;
    public PortalNodes PortalNodes;
    
    NativeArray<byte> _costs;
    NativeArray<DirectionData> _directions;
    AStarGrid _aStarGrid;
    public SectorGraph(int sectorSize, int totalTileAmount, int costFieldOffset, NativeArray<byte> costs, NativeArray<DirectionData> directions)
    {

        //size calculations
        int sectorMatrixSize = totalTileAmount / sectorSize;
        int sectorAmount = sectorMatrixSize * sectorMatrixSize;
        int windowAmount = sectorMatrixSize * ((sectorMatrixSize - 1) * 2);
        int winToSecPtrAmount = windowAmount * 2;
        int secToWinPtrAmount = windowAmount * 2;
        int divider = 2;
        for (int i = 0; i < costFieldOffset; i++)
        {
            divider *= 2;
        }
        int portalPerWindow = (sectorSize + divider - 1) / divider;
        int portalAmount = windowAmount * portalPerWindow;
        int porToPorPtrAmount = portalAmount * (portalPerWindow * 7 - 1);

        //innitialize fields
        _costs = costs;
        _directions = directions;
        _aStarGrid = new AStarGrid(_costs, _directions, totalTileAmount);
        SectorNodes = new SectorNodes(sectorAmount, secToWinPtrAmount);
        WindowNodes = new WindowNodes(windowAmount, winToSecPtrAmount);
        PortalNodes = new PortalNodes(portalAmount, porToPorPtrAmount);

        //configuring fields
        SectorNodes.ConfigureSectorNodes(totalTileAmount, sectorSize);
        WindowNodes.ConfigureWindowNodes(SectorNodes.Nodes, _costs, portalPerWindow, sectorMatrixSize, totalTileAmount);
        SectorNodes.ConfigureSectorToWindowPoiners(WindowNodes.Nodes);
        WindowNodes.ConfigureWindowToSectorPointers(SectorNodes.Nodes);
        PortalNodes.ConfigurePortalNodes(WindowNodes.Nodes, _costs, totalTileAmount);

    }
    public WindowNode[] GetWindowNodesOf(SectorNode sectorNode)
    {
        WindowNode[] windowNodes = new WindowNode[sectorNode.SecToWinCnt];
        for(int i = sectorNode.SecToWinPtr; i < sectorNode.SecToWinPtr + sectorNode.SecToWinCnt; i++)
        {
            windowNodes[i - sectorNode.SecToWinPtr] = WindowNodes.Nodes[SectorNodes.WinPtrs[i]];
        }
        return windowNodes;
    }
    public SectorNode[] GetSectorNodesOf(WindowNode windowNode)
    {
        SectorNode[] sectorNodes = new SectorNode[windowNode.WinToSecCnt];
        for (int i = windowNode.WinToSecPtr; i < windowNode.WinToSecPtr + windowNode.WinToSecCnt; i++)
        {
            sectorNodes[i - windowNode.WinToSecPtr] = SectorNodes.Nodes[WindowNodes.SecPtrs[i]];
        }
        return sectorNodes;
    }
    NativeArray<int> GetPortalIndiciesOf(SectorNode sectorNode)
    {
        NativeArray<int> portalIndicies;
        int portalIndexCount = 0;
        for(int i = 0; i < sectorNode.SecToWinCnt; i++)
        {
            portalIndexCount += WindowNodes.Nodes[sectorNode.SecToWinPtr + i].PorCnt;
        }
        portalIndicies = new NativeArray<int>(portalIndexCount, Allocator.Temp);

        int portalIndiciesIterable = 0;
        for(int i = 0; i < sectorNode.SecToWinCnt; i++)
        {
            int windowPorPtr = WindowNodes.Nodes[sectorNode.SecToWinPtr + i].PorPtr;
            int windowPorCnt = WindowNodes.Nodes[sectorNode.SecToWinPtr + i].PorCnt;
            for (int j = 0; j < windowPorCnt; j++)
            {
                portalIndicies[portalIndiciesIterable] = windowPorPtr + i;
            }
        }
        return portalIndicies;
    }
}

