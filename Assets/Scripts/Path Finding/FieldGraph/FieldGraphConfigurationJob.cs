using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

//Here, some vanilla procedural porgramming
[BurstCompile]
public struct FieldGraphConfigurationJob : IJob
{
    public NativeArray<SectorNode> SectorNodes;
    public NativeArray<int> WinPtrs;
    public NativeArray<WindowNode> WindowNodes;
    public NativeArray<int> SecPtrs;
    public NativeArray<PortalNode> PortalNodes;
    public NativeArray<PortalToPortal> PorPtrs;
    
    public NativeArray<byte> _costs;
    public NativeArray<DirectionData> _directions;

    public int _fieldTileAmount;
    public float _fieldTileSize;
    public int _sectorTileAmount;
    public int _sectorMatrixSize;
    public int _portalPerWindow;

    public NativeArray<AStarTile> _integratedCosts;
    public NativeQueue<int> _searchQueue;


    public void Execute()
    {
        ConfigureSectorNodes(_fieldTileAmount, _sectorTileAmount);
        ConfigureWindowNodes(_portalPerWindow, _sectorMatrixSize, _fieldTileAmount);
        ConfigureSectorToWindowPoiners();
        ConfigureWindowToSectorPointers();
        ConfigurePortalNodes(_fieldTileAmount, _portalPerWindow * 8 - 2);
        ConfigurePortalToPortalPtrs(_fieldTileAmount);
    }
    void ConfigureSectorNodes(int totalTileAmount, int sectorSize)
    {
        int sectorMatrixSize = totalTileAmount / sectorSize;

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
                SectorNodes[index] = new SectorNode(sect, secToWinCnt, iterableSecToWinPtr);
                iterableSecToWinPtr += secToWinCnt;
            }
        }
    }
    void ConfigureSectorToWindowPoiners()
    {
        int sectorSize = SectorNodes[0].Sector.Size;
        int secToWinPtrIterable = 0;
        for (int i = 0; i < SectorNodes.Length; i++)
        {
            Index2 sectorStartIndex = SectorNodes[i].Sector.StartIndex;
            Index2 topWinIndex = new Index2(sectorStartIndex.R + sectorSize - 1, sectorStartIndex.C);
            Index2 rightWinIndex = new Index2(sectorStartIndex.R, sectorStartIndex.C + sectorSize - 1);
            Index2 botWinIndex = new Index2(sectorStartIndex.R - 1, sectorStartIndex.C);
            Index2 leftWinIndex = new Index2(sectorStartIndex.R, sectorStartIndex.C - 1);
            for (int j = 0; j < WindowNodes.Length; j++)
            {
                Window window = WindowNodes[j].Window;
                if (window.BottomLeftBoundary == topWinIndex) { WinPtrs[secToWinPtrIterable++] = j; }
                else if (window.BottomLeftBoundary == rightWinIndex) { WinPtrs[secToWinPtrIterable++] = j; }
                else if (window.BottomLeftBoundary == botWinIndex) { WinPtrs[secToWinPtrIterable++] = j; }
                else if (window.BottomLeftBoundary == leftWinIndex) { WinPtrs[secToWinPtrIterable++] = j; }
            }
        }
    }
    public void ConfigureWindowNodes(int portalPerWindow, int sectorMatrixSize, int totalTileAmount)
    {
        int porPtrJumpFactor = portalPerWindow;
        int windowNodesIndex = 0;
        int iterableWinToSecPtr = 0;
        for (int r = 0; r < sectorMatrixSize; r++)
        {
            for (int c = 0; c < sectorMatrixSize; c++)
            {
                int index = r * sectorMatrixSize + c;
                Sector sector = SectorNodes[index].Sector;

                //create upper window relative to the sector
                if (!sector.IsOnTop(totalTileAmount))
                {
                    Window window = GetUpperWindowFor(sector);
                    WindowNodes[windowNodesIndex] = new WindowNode(window, 2, iterableWinToSecPtr, windowNodesIndex * porPtrJumpFactor, totalTileAmount, _costs);
                    windowNodesIndex++;
                    iterableWinToSecPtr += 2;
                }

                //create right window relative to the sector
                if (!sector.IsOnRight(totalTileAmount))
                {
                    Window window = GetRightWindowFor(sector);
                    WindowNodes[windowNodesIndex] = new WindowNode(window, 2, iterableWinToSecPtr, windowNodesIndex * porPtrJumpFactor, totalTileAmount, _costs);
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
    public void ConfigureWindowToSectorPointers()
    {
        int winToSecPtrIterable = 0;
        for (int i = 0; i < WindowNodes.Length; i++)
        {
            Index2 botLeft = WindowNodes[i].Window.BottomLeftBoundary;
            Index2 topRight = WindowNodes[i].Window.TopRightBoundary;
            for (int j = 0; j < SectorNodes.Length; j++)
            {
                if (SectorNodes[j].Sector.ContainsIndex(botLeft))
                {
                    SecPtrs[winToSecPtrIterable++] = j;
                }
                else if (SectorNodes[j].Sector.ContainsIndex(topRight))
                {
                    SecPtrs[winToSecPtrIterable++] = j;
                }
            }
        }
    }
    public void ConfigurePortalNodes(int tileAmount, int porToPorCnt)
    {
        NativeArray<WindowNode> windowNodes = WindowNodes;
        NativeArray<byte> costs = _costs;
        NativeArray<PortalNode> portalNodes = PortalNodes;

        for (int i = 0; i < windowNodes.Length; i++)
        {
            Window window = windowNodes[i].Window;
            if (window.IsHorizontal())
            {
                ConfigureForHorizontal();
            }
            else
            {
                ConfigureForVertical();
            }
            void ConfigureForHorizontal()
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
                    if (costs[index1] != byte.MaxValue && costs[index2] != byte.MaxValue)
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
                    if ((costs[index1] == byte.MaxValue || costs[index2] == byte.MaxValue) && !wasUnwalkable)
                    {
                        Portal portal = GetPortalBetween(bound1, bound2, true);
                        portalNodes[porPtr + portalCount] = new PortalNode(portal, i, (porPtr + portalCount) * porToPorCnt);
                        portalCount++;
                        wasUnwalkable = true;
                    }
                }
                if (!wasUnwalkable)
                {
                    Portal portal = GetPortalBetween(bound1, bound2, true);
                    portalNodes[porPtr + portalCount] = new PortalNode(portal, i, (porPtr + portalCount) * porToPorCnt);
                }
            }
            void ConfigureForVertical()
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
                        portalNodes[porPtr + portalCount] = new PortalNode(portal, i, (porPtr + portalCount) * porToPorCnt);
                        portalCount++;
                        wasUnwalkable = true;
                    }
                }
                if (!wasUnwalkable)
                {
                    Portal portal = GetPortalBetween(bound1, bound2, false);
                    portalNodes[porPtr + portalCount] = new PortalNode(portal, i, (porPtr + portalCount) * porToPorCnt);
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
    public void ConfigurePortalToPortalPtrs(int tileAmount)
    {
        NativeArray<SectorNode> sectorNodes = SectorNodes;

        for (int i = 0; i < sectorNodes.Length; i++)
        {
            Sector pickedSector = sectorNodes[i].Sector;
            NativeArray<int> portalIndicies = GetPortalIndicies(sectorNodes[i]);
            for (int j = 0; j < portalIndicies.Length; j++)
            {
                //for each portal, set it "target" and calculate distances of others
                PortalNode sourcePortalNode = PortalNodes[portalIndicies[j]];
                Portal sourcePortal = sourcePortalNode.Portal;
                Index2 sourceIndex = pickedSector.ContainsIndex(sourcePortal.Index1) ? sourcePortal.Index1 : sourcePortal.Index2;
                NativeArray<AStarTile> integratedCosts = GetIntegratedCostsFor(pickedSector, sourceIndex);

                for (int k = j + 1; k < portalIndicies.Length; k++)
                {
                    PortalNode targetPortalNode = PortalNodes[portalIndicies[k]];
                    Portal targetPortal = targetPortalNode.Portal;
                    Index2 targetIndex2 = pickedSector.ContainsIndex(targetPortal.Index1) ? targetPortal.Index1 : targetPortal.Index2;
                    int targetIndex = Index2.ToIndex(targetIndex2, tileAmount);
                    float cost = integratedCosts[targetIndex].IntegratedCost;

                    if (cost == float.MaxValue) { continue; }

                    //set for target
                    targetPortalNode.PorToPorCnt++;
                    PortalNodes[portalIndicies[k]] = targetPortalNode;
                    PorPtrs[targetPortalNode.PorToPorPtr + targetPortalNode.PorToPorCnt - 1] = new PortalToPortal(cost, portalIndicies[j]);

                    //set for source
                    sourcePortalNode.PorToPorCnt++;
                    PorPtrs[sourcePortalNode.PorToPorPtr + sourcePortalNode.PorToPorCnt - 1] = new PortalToPortal(cost, portalIndicies[k]);
                }
                PortalNodes[portalIndicies[j]] = sourcePortalNode;
            }
        }
    }
    public NativeArray<int> GetPortalIndicies(SectorNode sectorNode)
    {
        NativeArray<int> portalIndicies;
        int secToWinCnt = sectorNode.SecToWinCnt;
        int secToWinPtr = sectorNode.SecToWinPtr;

        //determine portal count
        int portalIndexCount = 0;
        for (int i = 0; i < secToWinCnt; i++)
        {
            portalIndexCount += WindowNodes[WinPtrs[secToWinPtr + i]].PorCnt;
        }
        portalIndicies = new NativeArray<int>(portalIndexCount, Allocator.Temp);

        //get portals
        int portalIndiciesIterable = 0;
        for (int i = 0; i < secToWinCnt; i++)
        {
            int windowPorPtr = WindowNodes[WinPtrs[secToWinPtr + i]].PorPtr;
            int windowPorCnt = WindowNodes[WinPtrs[secToWinPtr + i]].PorCnt;
            for (int j = 0; j < windowPorCnt; j++)
            {
                portalIndicies[portalIndiciesIterable++] = windowPorPtr + j;
            }
        }
        return portalIndicies;
    }
    public NativeArray<AStarTile> GetIntegratedCostsFor(Sector sector, Index2 target)
    {
        Reset(sector);
        int targetIndex = Index2.ToIndex(target, _fieldTileAmount);

        AStarTile targetTile = _integratedCosts[targetIndex];
        targetTile.IntegratedCost = 0f;
        targetTile.Enqueued = true;
        _integratedCosts[targetIndex] = targetTile;
        Enqueue(_directions[targetIndex]);

        while (!_searchQueue.IsEmpty())
        {
            int index = _searchQueue.Dequeue();
            AStarTile tile = _integratedCosts[index];
            tile.IntegratedCost = GetCost(_directions[index]);
            _integratedCosts[index] = tile;
            Enqueue(_directions[index]);
        }
        return _integratedCosts;
    }
    public void Reset(Sector sector)
    {
        Index2 lowerBound = sector.StartIndex;
        Index2 upperBound = new Index2(sector.StartIndex.R + sector.Size - 1, sector.StartIndex.C + sector.Size - 1);
        int lowerBoundIndex = Index2.ToIndex(lowerBound, _fieldTileAmount);
        int upperBoundIndex = Index2.ToIndex(upperBound, _fieldTileAmount);

        for (int r = lowerBoundIndex; r < lowerBoundIndex + sector.Size * _fieldTileAmount; r += _fieldTileAmount)
        {
            for (int i = r; i < r + sector.Size; i++)
            {
                if (_costs[i] == byte.MaxValue)
                {
                    _integratedCosts[i] = new AStarTile(float.MaxValue, true);
                    continue;
                }
                _integratedCosts[i] = new AStarTile(float.MaxValue, false);
            }
        }
        SetEdgesUnwalkable(sector, lowerBoundIndex, upperBoundIndex);
    }
    void SetEdgesUnwalkable(Sector sector, int lowerBoundIndex, int upperBoundIndex)
    {
        bool notOnBottom = !sector.IsOnBottom();
        bool notOnTop = !sector.IsOnTop(_fieldTileAmount);
        bool notOnRight = !sector.IsOnRight(_fieldTileAmount);
        bool notOnLeft = !sector.IsOnLeft();
        if (notOnBottom)
        {
            for (int i = lowerBoundIndex - _fieldTileAmount; i < (lowerBoundIndex - _fieldTileAmount) + sector.Size; i++)
            {
                _integratedCosts[i] = new AStarTile(float.MaxValue, true);
            }
        }
        if (notOnTop)
        {
            for (int i = upperBoundIndex + _fieldTileAmount; i > upperBoundIndex + _fieldTileAmount - sector.Size; i--)
            {
                _integratedCosts[i] = new AStarTile(float.MaxValue, true);
            }
        }
        if (notOnRight)
        {
            for (int i = upperBoundIndex + 1; i >= lowerBoundIndex + sector.Size; i -= _fieldTileAmount)
            {
                _integratedCosts[i] = new AStarTile(float.MaxValue, true);
            }
        }
        if (notOnLeft)
        {
            for (int i = lowerBoundIndex - 1; i <= upperBoundIndex - sector.Size; i += _fieldTileAmount)
            {
                _integratedCosts[i] = new AStarTile(float.MaxValue, true);
            }
        }
        if (notOnRight && notOnBottom)
        {
            _integratedCosts[lowerBoundIndex + sector.Size - _fieldTileAmount] = new AStarTile(float.MaxValue, true);
        }
        if (notOnRight && notOnTop)
        {
            _integratedCosts[upperBoundIndex + _fieldTileAmount + 1] = new AStarTile(float.MaxValue, true);
        }
        if (notOnLeft && notOnBottom)
        {
            _integratedCosts[lowerBoundIndex - _fieldTileAmount - 1] = new AStarTile(float.MaxValue, true);
        }
        if (notOnLeft && notOnTop)
        {
            _integratedCosts[upperBoundIndex + _fieldTileAmount - sector.Size] = new AStarTile(float.MaxValue, true);
        }
    }
    void Enqueue(DirectionData directions)
    {
        int n = directions.N;
        int e = directions.E;
        int s = directions.S;
        int w = directions.W;
        if (!_integratedCosts[n].Enqueued)
        {
            _searchQueue.Enqueue(n);
            AStarTile tile = _integratedCosts[n];
            tile.Enqueued = true;
            _integratedCosts[n] = tile;
        }
        if (!_integratedCosts[e].Enqueued)
        {
            _searchQueue.Enqueue(e);
            AStarTile tile = _integratedCosts[e];
            tile.Enqueued = true;
            _integratedCosts[e] = tile;
        }
        if (!_integratedCosts[s].Enqueued)
        {
            _searchQueue.Enqueue(s);
            AStarTile tile = _integratedCosts[s];
            tile.Enqueued = true;
            _integratedCosts[s] = tile;
        }
        if (!_integratedCosts[w].Enqueued)
        {
            _searchQueue.Enqueue(w);
            AStarTile tile = _integratedCosts[w];
            tile.Enqueued = true;
            _integratedCosts[w] = tile;
        }
    }
    float GetCost(DirectionData directions)
    {
        float costToReturn = float.MaxValue;
        float nCost = _integratedCosts[directions.N].IntegratedCost + 1f;
        float neCost = _integratedCosts[directions.NE].IntegratedCost + 1.4f;
        float eCost = _integratedCosts[directions.E].IntegratedCost + 1f;
        float seCost = _integratedCosts[directions.SE].IntegratedCost + 1.4f;
        float sCost = _integratedCosts[directions.S].IntegratedCost + 1f;
        float swCost = _integratedCosts[directions.SW].IntegratedCost + 1.4f;
        float wCost = _integratedCosts[directions.W].IntegratedCost + 1f;
        float nwCost = _integratedCosts[directions.NW].IntegratedCost + 1.4f;
        if (nCost < costToReturn) { costToReturn = nCost; }
        if (neCost < costToReturn) { costToReturn = neCost; }
        if (eCost < costToReturn) { costToReturn = eCost; }
        if (seCost < costToReturn) { costToReturn = seCost; }
        if (sCost < costToReturn) { costToReturn = sCost; }
        if (swCost < costToReturn) { costToReturn = swCost; }
        if (wCost < costToReturn) { costToReturn = wCost; }
        if (nwCost < costToReturn) { costToReturn = nwCost; }
        return costToReturn;
    }
}
