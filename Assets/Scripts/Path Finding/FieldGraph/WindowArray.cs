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
    public void ConfigureWindowNodes(NativeArray<SectorNode> sectorNodes, NativeArray<byte> costs, int portalPerWindow, int sectorMatrixSize, int totalTileAmount)
    {
        int porPtrJumpFactor = portalPerWindow;
        int windowNodesIndex = 0;
        int iterableWinToSecPtr = 0;
        for (int r = 0; r < sectorMatrixSize; r++)
        {
            for (int c = 0; c < sectorMatrixSize; c++)
            {
                int index = r * sectorMatrixSize + c;
                Sector sector = sectorNodes[index].Sector;

                //create upper window relative to the sector
                if (!sector.IsOnTop(totalTileAmount))
                {
                    Window window = GetUpperWindowFor(sector);
                    Nodes[windowNodesIndex] = new WindowNode(window, 2, iterableWinToSecPtr, windowNodesIndex * porPtrJumpFactor, totalTileAmount, costs);
                    windowNodesIndex++;
                    iterableWinToSecPtr += 2;
                }

                //create right window relative to the sector
                if (!sector.IsOnRight(totalTileAmount))
                {
                    Window window = GetRightWindowFor(sector);
                    Nodes[windowNodesIndex] = new WindowNode(window, 2, iterableWinToSecPtr, windowNodesIndex * porPtrJumpFactor, totalTileAmount, costs);
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
    public void ConfigureWindowToSectorPointers(NativeArray<SectorNode> sectorNodes)
    {
        int winToSecPtrIterable = 0;
        for (int i = 0; i < Nodes.Length; i++)
        {
            Index2 botLeft = Nodes[i].Window.BottomLeftBoundary;
            Index2 topRight = Nodes[i].Window.TopRightBoundary;
            for (int j = 0; j < sectorNodes.Length; j++)
            {
                if (sectorNodes[j].Sector.ContainsIndex(botLeft))
                {
                    SecPtrs[winToSecPtrIterable++] = j;
                }
                else if (sectorNodes[j].Sector.ContainsIndex(topRight))
                {
                    SecPtrs[winToSecPtrIterable++] = j;
                }
            }
        }
    }
}
