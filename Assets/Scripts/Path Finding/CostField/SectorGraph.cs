using JetBrains.Annotations;
using Unity.Collections;

public struct SectorGraph
{
    public NativeArray<SectorNode> SectorNodes;
    public NativeArray<WindowNode> WindowNodes;
    public NativeArray<PortalNode> PortalNodes;

    NativeArray<int> _secToWinPtrs;
    NativeArray<int> _winToSecPtrs;
    NativeArray<PortalToPortal> _porToPorPtrs;
    
    NativeArray<byte> _costs;
    NativeArray<DirectionData> _directions;
    AStarGrid _aStarGrid;
    public SectorGraph(int sectorSize, int totalTileAmount, int costFieldOffset, NativeArray<byte> costs, NativeArray<DirectionData> directions)
    {
        int sectorMatrixSize = totalTileAmount / sectorSize;
        int sectorTotalSize = sectorMatrixSize * sectorMatrixSize;
        int windowNodesSize = sectorMatrixSize * ((sectorMatrixSize - 1) * 2);
        int divider = 2;
        for (int i = 0; i < costFieldOffset; i++)
        {
            divider *= 2;
        }
        int portalPerWindow = (sectorSize + divider - 1) / divider;
        int portalNodesSize = windowNodesSize * portalPerWindow;
        int winToSecPtrsSize = windowNodesSize * 2;
        int secToWinPtrsSize = windowNodesSize * 2;
        int porToPorPtrsSize = portalNodesSize * portalPerWindow * 7 - 1;

        //innitialize fields
        _costs = costs;
        SectorNodes = new NativeArray<SectorNode>(sectorTotalSize, Allocator.Persistent);
        WindowNodes = new NativeArray<WindowNode>(windowNodesSize, Allocator.Persistent);
        PortalNodes = new NativeArray<PortalNode>(portalNodesSize, Allocator.Persistent);
        _winToSecPtrs = new NativeArray<int>(winToSecPtrsSize, Allocator.Persistent);
        _secToWinPtrs = new NativeArray<int>(secToWinPtrsSize, Allocator.Persistent);
        _porToPorPtrs = new NativeArray<PortalToPortal>(porToPorPtrsSize, Allocator.Persistent);
        _directions = directions;
        _aStarGrid = new AStarGrid(costs, directions, totalTileAmount);

        //configuring fields
        ConfigureSectorNodes(ref SectorNodes);
        ConfigureWindowNodes(ref WindowNodes, ref SectorNodes, ref _costs);
        ConfigureSectorToWindowPoiners(ref SectorNodes, ref WindowNodes, ref _secToWinPtrs);
        ConfigureWindowToSectorPointers(ref SectorNodes, ref WindowNodes, ref _winToSecPtrs);
        ConfigurePortalNodes(ref PortalNodes, ref WindowNodes, ref _costs, totalTileAmount);

        //HELPERS
        void ConfigureSectorNodes(ref NativeArray<SectorNode> sectorNodes)
        {
            int sectorMatrixSize = totalTileAmount / sectorSize;
            int sectorTotalSize = sectorMatrixSize * sectorMatrixSize;

            sectorNodes = new NativeArray<SectorNode>(sectorTotalSize, Allocator.Persistent);
            int iterableSecToWinPtr = 0;
            for (int r = 0; r < sectorMatrixSize; r++)
            {
                for (int c = 0; c < sectorMatrixSize; c++)
                {
                    int index = r * sectorMatrixSize + c;
                    Sector sect = new Sector(new Index2(r * sectorSize, c * sectorSize), sectorSize);
                    int secToWinCnt = 4;
                    if (sect.IsOnCorner(totalTileAmount))
                    {
                        secToWinCnt = 2;
                    }
                    else if (sect.IsOnEdge(totalTileAmount))
                    {
                        secToWinCnt = 3;
                    }
                    sectorNodes[index] = new SectorNode(sect, secToWinCnt, iterableSecToWinPtr);
                    iterableSecToWinPtr += secToWinCnt;
                }
            }
        }
        void ConfigureWindowNodes(ref NativeArray<WindowNode> windowNodes, ref NativeArray<SectorNode> helperSectorNodes, ref NativeArray<byte> helperCosts)
        {
            int porPtrJumpFactor = portalPerWindow;
            int windowNodesIndex = 0;
            int iterableWinToSecPtr = 0;
            for (int r = 0; r < sectorMatrixSize; r++)
            {
                for (int c = 0; c < sectorMatrixSize; c++)
                {
                    int index = r * sectorMatrixSize + c;
                    Sector sector = helperSectorNodes[index].Sector;

                    //create upper window relative to the sector
                    if (!sector.IsOnTop(totalTileAmount))
                    {
                        Window window = GetUpperWindowFor(sector);
                        windowNodes[windowNodesIndex] = new WindowNode(window, 2, iterableWinToSecPtr, windowNodesIndex * porPtrJumpFactor, totalTileAmount, helperCosts);
                        windowNodesIndex++;
                        iterableWinToSecPtr += 2;
                    }

                    //create right window relative to the sector
                    if (!sector.IsOnRight(totalTileAmount))
                    {
                        Window window = GetRightWindowFor(sector);
                        windowNodes[windowNodesIndex] = new WindowNode(window, 2, iterableWinToSecPtr, windowNodesIndex * porPtrJumpFactor, totalTileAmount, helperCosts);
                        windowNodesIndex++;
                        iterableWinToSecPtr += 2;
                    }
                }
            }
            Window GetUpperWindowFor(Sector sector)
            {
                Index2 bottomLeftBoundary = new Index2(sector.StartIndex.R + sector.Size - 1, sector.StartIndex.C);
                Index2 topRightBoundary = new Index2(sector.StartIndex.R + sector.Size, sector.StartIndex.C + sector.Size - 1);
                return new Window(bottomLeftBoundary, topRightBoundary);
            }
            Window GetRightWindowFor(Sector sector)
            {
                Index2 bottomLeftBoundary = new Index2(sector.StartIndex.R, sector.StartIndex.C + sector.Size - 1);
                Index2 topRightBoundary = new Index2(bottomLeftBoundary.R + sector.Size - 1, bottomLeftBoundary.C + 1);
                return new Window(bottomLeftBoundary, topRightBoundary);
            }
        }       
        void ConfigureSectorToWindowPoiners(ref NativeArray<SectorNode> sectorNodes, ref NativeArray<WindowNode> windowNodes, ref NativeArray<int> secToWınPointers)
        {
            int sectorSize = sectorNodes[0].Sector.Size;
            int secToWinPtrIterable = 0;
            for(int i = 0; i < sectorNodes.Length; i++)
            {
                Index2 sectorStartIndex = sectorNodes[i].Sector.StartIndex;
                Index2 topWinIndex = new Index2(sectorStartIndex.R + sectorSize - 1, sectorStartIndex.C);
                Index2 rightWinIndex = new Index2(sectorStartIndex.R, sectorStartIndex.C + sectorSize - 1);
                Index2 botWinIndex = new Index2(sectorStartIndex.R - 1, sectorStartIndex.C);
                Index2 leftWinIndex = new Index2(sectorStartIndex.R, sectorStartIndex.C - 1);
                for (int j = 0; j < windowNodes.Length; j++)
                {
                    Window window = windowNodes[j].Window;
                    if(window.BottomLeftBoundary == topWinIndex) { secToWınPointers[secToWinPtrIterable++] = j; }
                    else if(window.BottomLeftBoundary == rightWinIndex) { secToWınPointers[secToWinPtrIterable++] = j; }
                    else if(window.BottomLeftBoundary == botWinIndex) { secToWınPointers[secToWinPtrIterable++] = j; }
                    else if(window.BottomLeftBoundary == leftWinIndex) { secToWınPointers[secToWinPtrIterable++] = j; }
                }
            }
        }
        void ConfigureWindowToSectorPointers(ref NativeArray<SectorNode> sectorNodes, ref NativeArray<WindowNode> windowNodes, ref NativeArray<int> winToSecPointers)
        {
            int winToSecPtrIterable = 0;
            for (int i = 0; i < windowNodes.Length; i++)
            {
                Index2 botLeft = windowNodes[i].Window.BottomLeftBoundary;
                Index2 topRight = windowNodes[i].Window.TopRightBoundary;
                for(int j = 0; j < sectorNodes.Length; j++)
                {
                    if (sectorNodes[j].Sector.ContainsIndex(botLeft))
                    {
                        winToSecPointers[winToSecPtrIterable++] = j;
                    }
                    else if (sectorNodes[j].Sector.ContainsIndex(topRight))
                    {
                        winToSecPointers[winToSecPtrIterable++] = j;
                    }
                }
            }
        }
        void ConfigurePortalNodes(ref NativeArray<PortalNode> portalNodes, ref NativeArray<WindowNode> windowNodes, ref NativeArray<byte> costs, int tileAmount)
        {
            for (int i = 0; i < windowNodes.Length; i++)
            {
                Window window = windowNodes[i].Window;
                if (window.IsHorizontal())
                {
                    int porPtr = windowNodes[i].PorPtr;
                    int portalCount = 0;
                    bool wasUnwalkable = true;
                    Index2 bound1 = new Index2();
                    Index2 bound2 = new Index2();
                    int startCol = window.BottomLeftBoundary.C;
                    int lastCol = window.TopRightBoundary.C;
                    int row1 = window.BottomLeftBoundary.R;
                    int row2 = window.TopRightBoundary.R;
                    for (int j = startCol; j <= lastCol; j++)
                    {
                        int index1 = row1 * tileAmount + j;
                        int index2 = row2 * tileAmount + j;
                        if (costs[index1]!=byte.MaxValue && costs[index2] != byte.MaxValue)
                        {
                            if (wasUnwalkable)
                            {
                                bound1 = new Index2(row1, j);
                                bound2 = new Index2(row2, j);
                                wasUnwalkable = false;
                            }
                            else
                            {
                                bound2 = new Index2(row2, j);
                            }
                        }
                        if((costs[index1] == byte.MaxValue || costs[index2] == byte.MaxValue) && !wasUnwalkable)
                        {
                            Portal portal = GetPortalBetween(bound1 , bound2, true);
                            portalNodes[porPtr + portalCount] = new PortalNode(portal, i);
                            portalCount++;
                            wasUnwalkable = true;
                        }
                    }
                    if (!wasUnwalkable)
                    {
                        Portal portal = GetPortalBetween(bound1, bound2, true);
                        portalNodes[porPtr + portalCount] = new PortalNode(portal, i);
                    }
                }
                else
                {
                    int porPtr = windowNodes[i].PorPtr;
                    int portalCount = 0;
                    bool wasUnwalkable = true;
                    Index2 bound1 = new Index2();
                    Index2 bound2 = new Index2();
                    int startRow = window.BottomLeftBoundary.R;
                    int lastRow = window.TopRightBoundary.R;
                    int col1 = window.BottomLeftBoundary.C;
                    int col2 = window.TopRightBoundary.C;
                    for (int j = startRow; j <= lastRow; j++)
                    {
                        int index1 = j * tileAmount + col1;
                        int index2 = j * tileAmount + col2;
                        if (costs[index1] != byte.MaxValue && costs[index2] != byte.MaxValue)
                        {
                            if (wasUnwalkable)
                            {
                                bound1 = new Index2(j, col1);
                                bound2 = new Index2(j, col2);
                                wasUnwalkable = false;
                            }
                            else
                            {
                                bound2 = new Index2(j, col2);
                            }
                        }
                        if ((costs[index1] == byte.MaxValue || costs[index2] == byte.MaxValue) && !wasUnwalkable)
                        {
                            Portal portal = GetPortalBetween(bound1, bound2, false);
                            portalNodes[porPtr + portalCount] = new PortalNode(portal, i);
                            portalCount++;
                            wasUnwalkable = true;
                        }
                    }
                    if (!wasUnwalkable)
                    {
                        Portal portal = GetPortalBetween(bound1, bound2, false);
                        portalNodes[porPtr + portalCount] = new PortalNode(portal, i);
                    }
                }
            }
            Portal GetPortalBetween(Index2 boundary1, Index2 boundary2, bool isHorizontal)
            {
                Portal portal;
                if (isHorizontal)
                {
                    int col = (boundary1.C + boundary2.C) / 2;
                    portal = new Portal(new Index2(boundary1.R, col), new Index2(boundary2.R, col));
                }
                else
                {
                    int row = (boundary1.R + boundary2.R) / 2;
                    portal = new Portal(new Index2(row, boundary1.C), new Index2(row, boundary2.C));
                }
                return portal;
            }
        }
        
    }
    public WindowNode[] GetWindowNodesOf(SectorNode sectorNode)
    {
        WindowNode[] windowNodes = new WindowNode[sectorNode.SecToWinCnt];
        for(int i = sectorNode.SecToWinPtr; i < sectorNode.SecToWinPtr + sectorNode.SecToWinCnt; i++)
        {
            windowNodes[i - sectorNode.SecToWinPtr] = WindowNodes[_secToWinPtrs[i]];
        }
        return windowNodes;
    }
    public SectorNode[] GetSectorNodesOf(WindowNode windowNode)
    {
        SectorNode[] sectorNodes = new SectorNode[windowNode.WinToSecCnt];
        for (int i = windowNode.WinToSecPtr; i < windowNode.WinToSecPtr + windowNode.WinToSecCnt; i++)
        {
            sectorNodes[i - windowNode.WinToSecPtr] = SectorNodes[_winToSecPtrs[i]];
        }
        return sectorNodes;
    }
    NativeArray<int> GetPortalIndiciesOf(SectorNode sectorNode)
    {
        NativeArray<int> portalIndicies;
        int portalIndexCount = 0;
        for(int i = 0; i < sectorNode.SecToWinCnt; i++)
        {
            portalIndexCount += WindowNodes[sectorNode.SecToWinPtr + i].PorCnt;
        }
        portalIndicies = new NativeArray<int>(portalIndexCount, Allocator.Temp);

        int portalIndiciesIterable = 0;
        for(int i = 0; i < sectorNode.SecToWinCnt; i++)
        {
            int windowPorPtr = WindowNodes[sectorNode.SecToWinPtr + i].PorPtr;
            int windowPorCnt = WindowNodes[sectorNode.SecToWinPtr + i].PorCnt;
            for (int j = 0; j < windowPorCnt; j++)
            {
                portalIndicies[portalIndiciesIterable] = windowPorPtr + i;
            }
        }
        return portalIndicies;
    }
}

