using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

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

    public int _fieldRowAmount;
    public int _fieldColAmount;
    public float _fieldTileSize;
    public int _sectorTileAmount;
    //public int _sectorMatrixSize;
    public int _sectorColAmount;
    public int _sectorRowAmount;
    public int _portalPerWindow;

    public NativeArray<AStarTile> _integratedCosts;
    public NativeQueue<int> _searchQueue;


    public void Execute()
    {
        ConfigureSectorNodes(_sectorTileAmount);
        ConfigureWindowNodes(_portalPerWindow);
        ConfigureSectorToWindowPoiners();
        ConfigureWindowToSectorPointers();
        ConfigurePortalNodes(_portalPerWindow * 8 - 2);
        ConfigurePortalToPortalPtrs();
    }
    void ConfigureSectorNodes(int sectorSize)
    {
        //int sectorMatrixSize = _sectorMatrixSize;

        int iterableSecToWinPtr = 0;
        for (int r = 0; r < _sectorRowAmount; r++)
        {
            for (int c = 0; c < _sectorColAmount; c++)
            {
                int index = r * _sectorColAmount + c;
                Sector sect = new Sector(new Index2(r * sectorSize, c * sectorSize), sectorSize);
                int secToWinCnt = 4;
                if (sect.IsOnCorner(_fieldColAmount, _fieldRowAmount))
                {
                    secToWinCnt = 2;
                }
                else if (sect.IsOnEdge(_fieldColAmount, _fieldRowAmount))
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
    void ConfigureWindowNodes(int portalPerWindow)
    {
        int porPtrJumpFactor = portalPerWindow;
        int windowNodesIndex = 0;
        int iterableWinToSecPtr = 0;
        for (int r = 0; r < _sectorRowAmount; r++)
        {
            for (int c = 0; c < _sectorColAmount; c++)
            {
                int index = r * _sectorColAmount + c;
                Sector sector = SectorNodes[index].Sector;

                //create upper window relative to the sector
                if (!sector.IsOnTop(_fieldRowAmount))
                {
                    Window window = GetUpperWindowFor(sector);
                    WindowNodes[windowNodesIndex] = new WindowNode(window, 2, iterableWinToSecPtr, windowNodesIndex * porPtrJumpFactor, GetPortalCountFor(window), _costs);
                    windowNodesIndex++;
                    iterableWinToSecPtr += 2;
                }

                //create right window relative to the sector
                if (!sector.IsOnRight(_fieldColAmount))
                {
                    Window window = GetRightWindowFor(sector);
                    WindowNodes[windowNodesIndex] = new WindowNode(window, 2, iterableWinToSecPtr, windowNodesIndex * porPtrJumpFactor, GetPortalCountFor(window), _costs);
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
    void ConfigureWindowToSectorPointers()
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
                    SecPtrs[winToSecPtrIterable] = j;
                }
                else if (SectorNodes[j].Sector.ContainsIndex(topRight))
                {
                    SecPtrs[winToSecPtrIterable + 1] = j;
                }
            }
            winToSecPtrIterable += 2;
        }
    }
    void ConfigurePortalNodes(int porToPorCnt)
    {
        int fieldColAmount = _fieldColAmount;
        int fieldRowAmount = _fieldRowAmount;
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
                    int index1 = row1 * fieldColAmount + j;
                    int index2 = row2 * fieldColAmount + j;
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
                        portalNodes[porPtr + portalCount] = GetPortalNodeBetween(bound1, bound2, porPtr, portalCount, i, true);
                        portalCount++;
                        wasUnwalkable = true;
                    }
                }
                if (!wasUnwalkable)
                {
                    portalNodes[porPtr + portalCount] = GetPortalNodeBetween(bound1, bound2, porPtr, portalCount, i, true);
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
                    int index1 = j * fieldColAmount + col1;
                    int index2 = j * fieldColAmount + col2;
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
                        portalNodes[porPtr + portalCount] = GetPortalNodeBetween(bound1, bound2, porPtr, portalCount, i, false);
                        portalCount++;
                        wasUnwalkable = true;
                    }
                }
                if (!wasUnwalkable)
                {
                    portalNodes[porPtr + portalCount] = GetPortalNodeBetween(bound1, bound2, porPtr, portalCount, i, false); ;
                }
            }
        }
        PortalNode GetPortalNodeBetween(Index2 boundary1, Index2 boundary2, int porPtr, int portalCount, int winPtr, bool isHorizontal)
        {
            Portal portal1;
            Portal portal2;
            int por1PorToPorPtr = (porPtr + portalCount) * porToPorCnt;
            int por2PorToPorPtr = por1PorToPorPtr + porToPorCnt / 2;
            if (isHorizontal)
            {
                int col = (boundary1.C + boundary2.C) / 2;
                portal1 = new Portal(new Index2(boundary1.R, col), por1PorToPorPtr);
                portal2 = new Portal(new Index2(boundary2.R, col), por2PorToPorPtr);
            }
            else
            {
                int row = (boundary1.R + boundary2.R) / 2;
                portal1 = new Portal(new Index2(row, boundary1.C), por1PorToPorPtr);
                portal2 = new Portal(new Index2(row, boundary2.C), por2PorToPorPtr);
            }
            return new PortalNode(portal1, portal2, winPtr);
        }
    }
    void ConfigurePortalToPortalPtrs()
    {

        int sectorColAmount = _sectorColAmount;
        int sectorTileAmount = _sectorTileAmount;
        NativeArray<PortalNode> portalNodes = PortalNodes;
        NativeArray<SectorNode> sectorNodes = SectorNodes;
        NativeArray<PortalToPortal> porPtrs = PorPtrs;
        int fieldColAmount = _fieldColAmount;

        for (int i = 0; i < sectorNodes.Length; i++)
        {
            Sector pickedSector = sectorNodes[i].Sector;
            NativeArray<int> portalIndicies = GetPortalIndicies(sectorNodes[i]);
            NativeArray<byte> portalDeterminationArray = GetPortalDeterminationArrayFor(portalIndicies, i);

            for (int j = 0; j < portalIndicies.Length; j++)
            {
                //for each portal, set it "target" and calculate distances of others
                PortalNode sourcePortalNode = PortalNodes[portalIndicies[j]];
                Index2 sourceIndex = portalDeterminationArray[j] == 1 ? sourcePortalNode.Portal1.Index : sourcePortalNode.Portal2.Index;
                NativeArray<AStarTile> integratedCosts = GetIntegratedCostsFor(pickedSector, sourceIndex);

                CalculatePortalBounds(0, j);
                CalculatePortalBounds(j + 1, portalIndicies.Length);

                void CalculatePortalBounds(int fromInclusive, int toExclusive)
                {
                    for (int k = fromInclusive; k < toExclusive; k++)
                    {
                        PortalNode targetPortalNode = portalNodes[portalIndicies[k]];
                        byte pickedTargetPortalNumber = portalDeterminationArray[k];
                        Index2 targetIndex = pickedTargetPortalNumber == 1 ? targetPortalNode.Portal1.Index : targetPortalNode.Portal2.Index;
                        int targetIndexFlat = targetIndex.R * fieldColAmount + targetIndex.C;

                        float cost = integratedCosts[targetIndexFlat].IntegratedCost;

                        if (cost == float.MaxValue) { continue; }

                        if (pickedTargetPortalNumber == 1)
                        {
                            targetPortalNode.Portal1.PorToPorCnt++;
                            portalNodes[portalIndicies[k]] = targetPortalNode;
                            porPtrs[targetPortalNode.Portal1.PorToPorPtr + targetPortalNode.Portal1.PorToPorCnt - 1] = new PortalToPortal(cost, portalIndicies[j]);
                        }
                        else
                        {
                            targetPortalNode.Portal2.PorToPorCnt++;
                            portalNodes[portalIndicies[k]] = targetPortalNode;
                            porPtrs[targetPortalNode.Portal2.PorToPorPtr + targetPortalNode.Portal2.PorToPorCnt - 1] = new PortalToPortal(cost, portalIndicies[j]);
                        }
                    }
                }
            }
        }
        NativeArray<byte> GetPortalDeterminationArrayFor(NativeArray<int> portalIndicies, int sectorIndex)
        {
            NativeArray<byte> determinationArray = new NativeArray<byte>(portalIndicies.Length, Allocator.Temp);
            for(int i = 0; i < determinationArray.Length; i++)
            {
                Portal portal1 = portalNodes[portalIndicies[i]].Portal1;
                Index2 sectorSpaceIndex = new Index2(portal1.Index.R / sectorTileAmount, portal1.Index.C / sectorTileAmount);
                determinationArray[i] = sectorIndex == sectorSpaceIndex.R * sectorColAmount + sectorSpaceIndex.C ? (byte) 1 : (byte) 2;
            }
            return determinationArray;
        }
    }
    
    NativeArray<int> GetPortalIndicies(SectorNode sectorNode)
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
    NativeArray<AStarTile> GetIntegratedCostsFor(Sector sector, Index2 target)
    {
        Reset(sector);
        int targetIndex = Index2.ToIndex(target, _fieldColAmount);

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
    void Reset(Sector sector)
    {
        Index2 lowerBound = sector.StartIndex;
        Index2 upperBound = new Index2(sector.StartIndex.R + sector.Size - 1, sector.StartIndex.C + sector.Size - 1);
        int lowerBoundIndex = Index2.ToIndex(lowerBound, _fieldColAmount);
        int upperBoundIndex = Index2.ToIndex(upperBound, _fieldColAmount);

        for (int r = lowerBoundIndex; r < lowerBoundIndex + sector.Size * _fieldColAmount; r += _fieldColAmount)
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
        bool notOnTop = !sector.IsOnTop(_fieldRowAmount);
        bool notOnRight = !sector.IsOnRight(_fieldColAmount);
        bool notOnLeft = !sector.IsOnLeft();
        if (notOnBottom)
        {
            for (int i = lowerBoundIndex - _fieldColAmount; i < (lowerBoundIndex - _fieldColAmount) + sector.Size; i++)
            {
                _integratedCosts[i] = new AStarTile(float.MaxValue, true);
            }
        }
        if (notOnTop)
        {
            for (int i = upperBoundIndex + _fieldColAmount; i > upperBoundIndex + _fieldColAmount - sector.Size; i--)
            {
                _integratedCosts[i] = new AStarTile(float.MaxValue, true);
            }
        }
        if (notOnRight)
        {
            for (int i = upperBoundIndex + 1; i >= lowerBoundIndex + sector.Size; i -= _fieldColAmount)
            {
                _integratedCosts[i] = new AStarTile(float.MaxValue, true);
            }
        }
        if (notOnLeft)
        {
            for (int i = lowerBoundIndex - 1; i <= upperBoundIndex - sector.Size; i += _fieldColAmount)
            {
                _integratedCosts[i] = new AStarTile(float.MaxValue, true);
            }
        }
        if (notOnRight && notOnBottom)
        {
            _integratedCosts[lowerBoundIndex + sector.Size - _fieldColAmount] = new AStarTile(float.MaxValue, true);
        }
        if (notOnRight && notOnTop)
        {
            _integratedCosts[upperBoundIndex + _fieldColAmount + 1] = new AStarTile(float.MaxValue, true);
        }
        if (notOnLeft && notOnBottom)
        {
            _integratedCosts[lowerBoundIndex - _fieldColAmount - 1] = new AStarTile(float.MaxValue, true);
        }
        if (notOnLeft && notOnTop)
        {
            _integratedCosts[upperBoundIndex + _fieldColAmount - sector.Size] = new AStarTile(float.MaxValue, true);
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
    int GetPortalCountFor(Window window)
    {
        if (window.IsHorizontal())  //if horizontal
        {
            int portalAmount = 0;
            bool wasUnwalkableFlag = true;
            int startCol = window.BottomLeftBoundary.C;
            int lastCol = window.TopRightBoundary.C;
            int row1 = window.BottomLeftBoundary.R;
            int row2 = window.TopRightBoundary.R;
            for (int i = startCol; i <= lastCol; i++)
            {
                int costIndex1 = row1 * _fieldColAmount + i;
                int costIndex2 = row2 * _fieldColAmount + i;
                if (_costs[costIndex1] == byte.MaxValue) { wasUnwalkableFlag = true; }
                else if (_costs[costIndex2] == byte.MaxValue) { wasUnwalkableFlag = true; }
                else if (wasUnwalkableFlag) { portalAmount++; wasUnwalkableFlag = false; }
            }
            return portalAmount;
        }
        else //if vertical
        {
            int portalAmount = 0;
            bool wasUnwalkableFlag = true;
            int startRow = window.BottomLeftBoundary.R;
            int lastRow = window.TopRightBoundary.R;
            int col1 = window.BottomLeftBoundary.C;
            int col2 = window.TopRightBoundary.C;
            for (int i = startRow; i <= lastRow; i++)
            {
                int costIndex1 = i * _fieldColAmount + col1;
                int costIndex2 = i * _fieldColAmount + col2;
                if (_costs[costIndex1] == byte.MaxValue) { wasUnwalkableFlag = true; }
                else if (_costs[costIndex2] == byte.MaxValue) { wasUnwalkableFlag = true; }
                else if (wasUnwalkableFlag) { portalAmount++; wasUnwalkableFlag = false; }
            }
            return portalAmount;
        }

    }
}
