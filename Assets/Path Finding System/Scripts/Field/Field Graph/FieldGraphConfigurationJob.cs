﻿using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using static UnityEngine.Rendering.DebugUI.Table;

[BurstCompile]
public struct FieldGraphConfigurationJob : IJob
{
    public int FieldRowAmount;
    public int FieldColAmount;
    public int SectorMatrixColAmount;
    public int SectorMatrixRowAmount;
    public int SectorColAmount;
    public int SectorRowAmount;
    public int SectorTileAmount;
    public int PortalPerWindow;
    public UnsafeList<SectorNode> SectorNodes;
    public NativeArray<int> SecToWinPtrs;
    public NativeArray<WindowNode> WindowNodes;
    public NativeArray<int> WinToSecPtrs;
    public UnsafeList<PortalNode> PortalNodes;
    public NativeArray<PortalToPortal> PorToPorPtrs;
    public NativeArray<byte> Costs;
    public NativeArray<AStarTile> IntegratedCosts;


    public void Execute()
    {
        ConfigureSectorNodes(SectorColAmount);
        ConfigureWindowNodes(PortalPerWindow);
        ConfigureSectorToWindowPoiners();
        ConfigureWindowToSectorPointers();
        ConfigurePortalNodes(PortalPerWindow * 8 - 2);
        ConfigurePortalToPortalPtrs();
    }
    void ConfigureSectorNodes(int sectorColAmount)
    {
        //int sectorMatrixSize = _sectorMatrixSize;

        int iterableSecToWinPtr = 0;
        for (int r = 0; r < SectorMatrixRowAmount; r++)
        {
            for (int c = 0; c < SectorMatrixColAmount; c++)
            {
                int index = r * SectorMatrixColAmount + c;
                Sector sect = new Sector(new Index2(r * sectorColAmount, c * sectorColAmount), sectorColAmount);
                int secToWinCnt = 4;
                if (sect.IsOnCorner(FieldColAmount, FieldRowAmount))
                {
                    secToWinCnt = 2;
                }
                else if (sect.IsOnEdge(FieldColAmount, FieldRowAmount))
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
                if (window.BottomLeftBoundary == topWinIndex) { SecToWinPtrs[secToWinPtrIterable++] = j; }
                else if (window.BottomLeftBoundary == rightWinIndex) { SecToWinPtrs[secToWinPtrIterable++] = j; }
                else if (window.BottomLeftBoundary == botWinIndex) { SecToWinPtrs[secToWinPtrIterable++] = j; }
                else if (window.BottomLeftBoundary == leftWinIndex) { SecToWinPtrs[secToWinPtrIterable++] = j; }
            }
        }
    }
    void ConfigureWindowNodes(int portalPerWindow)
    {
        //DATA

        int porPtrJumpFactor = portalPerWindow;
        NativeArray<byte> costField = Costs;
        int fieldColAmount = FieldColAmount;
        int sectorColAmount = SectorColAmount;
        int sectorRowAmount = SectorRowAmount;
        int sectorMatrixColAmount = SectorMatrixColAmount;
        int sectorTileAmount = SectorTileAmount;
        //CODE

        int windowNodesIndex = 0;
        int iterableWinToSecPtr = 0;
        for (int r = 0; r < SectorMatrixRowAmount; r++)
        {
            for (int c = 0; c < SectorMatrixColAmount; c++)
            {
                int index = r * SectorMatrixColAmount + c;
                Sector sector = SectorNodes[index].Sector;

                //create upper window relative to the sector
                if (!sector.IsOnTop(FieldRowAmount))
                {
                    Window window = GetUpperWindowFor(sector);
                    WindowNodes[windowNodesIndex] = new WindowNode(window, 2, iterableWinToSecPtr, windowNodesIndex * porPtrJumpFactor, GetPortalCountFor(window));
                    windowNodesIndex++;
                    iterableWinToSecPtr += 2;
                }

                //create right window relative to the sector
                if (!sector.IsOnRight(fieldColAmount))
                {
                    Window window = GetRightWindowFor(sector);
                    WindowNodes[windowNodesIndex] = new WindowNode(window, 2, iterableWinToSecPtr, windowNodesIndex * porPtrJumpFactor, GetPortalCountFor(window));
                    windowNodesIndex++;
                    iterableWinToSecPtr += 2;
                }
            }
        }
        
        //HELPERS

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
        int GetPortalCountFor(Window window)
        {
            if (window.IsHorizontal())  //if horizontal
            {
                int2 windowBotLeft = new int2(window.BottomLeftBoundary.C, window.BottomLeftBoundary.R);
                int2 windowTopRight = new int2(window.TopRightBoundary.C, window.TopRightBoundary.R);
                int sectorBot = FlowFieldUtilities.GetSector1D(windowBotLeft, sectorColAmount, sectorMatrixColAmount);
                int sectorTop = FlowFieldUtilities.GetSector1D(windowTopRight, sectorColAmount, sectorMatrixColAmount);
                int botStartIndex = sectorBot * sectorTileAmount + (sectorColAmount * (sectorRowAmount - 1));
                int topStartIndex = sectorTop * sectorTileAmount;

                int portalAmount = 0;
                bool wasUnwalkableFlag = true;
                for (int i = 0; i < sectorColAmount; i++)
                {
                    int botIndex = botStartIndex + i;
                    int topIndex = topStartIndex + i;
                    if (costField[botIndex] == byte.MaxValue) { wasUnwalkableFlag = true; }
                    else if (costField[topIndex] == byte.MaxValue) { wasUnwalkableFlag = true; }
                    else if (wasUnwalkableFlag) { portalAmount++; wasUnwalkableFlag = false; }
                }
                return portalAmount;
            }
            else //if vertical
            {
                int2 windowBotLeft = new int2(window.BottomLeftBoundary.C, window.BottomLeftBoundary.R);
                int2 windowTopRight = new int2(window.TopRightBoundary.C, window.TopRightBoundary.R);
                int sectorLeft = FlowFieldUtilities.GetSector1D(windowBotLeft, sectorColAmount, sectorMatrixColAmount);
                int sectorRight = FlowFieldUtilities.GetSector1D(windowTopRight, sectorColAmount, sectorMatrixColAmount);
                int leftStartIndex = sectorLeft * sectorTileAmount + sectorColAmount - 1;
                int rightStartIndex = sectorRight * sectorTileAmount;

                int portalAmount = 0;
                bool wasUnwalkableFlag = true;
                for (int i = 0; i < sectorRowAmount; i++)
                {
                    int leftIndex = leftStartIndex + i * sectorColAmount;
                    int rightIndex = rightStartIndex + i * sectorColAmount;
                    if (costField[leftIndex] == byte.MaxValue) { wasUnwalkableFlag = true; }
                    else if (costField[rightIndex] == byte.MaxValue) { wasUnwalkableFlag = true; }
                    else if (wasUnwalkableFlag) { portalAmount++; wasUnwalkableFlag = false; }
                }
                return portalAmount;
            }
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
                    WinToSecPtrs[winToSecPtrIterable] = j;
                }
                else if (SectorNodes[j].Sector.ContainsIndex(topRight))
                {
                    WinToSecPtrs[winToSecPtrIterable + 1] = j;
                }
            }
            winToSecPtrIterable += 2;
        }
    }
    void ConfigurePortalNodes(int porToPorCnt)
    {
        NativeArray<int> winToSecPtrs = WinToSecPtrs;
        int fieldColAmount = FieldColAmount;
        int fieldRowAmount = FieldRowAmount;
        int sectorColAmount = SectorColAmount;
        int sectorRowAmount = SectorRowAmount;
        int sectorTileAmount = SectorTileAmount;
        int sectorMatrixColAmount = SectorMatrixColAmount;
        NativeArray<WindowNode> windowNodes = WindowNodes;
        NativeArray<byte> costs = Costs;
        UnsafeList<PortalNode> portalNodes = PortalNodes;

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
                int2 windowBotLeft = new int2(window.BottomLeftBoundary.C, window.BottomLeftBoundary.R);
                int2 windowTopRight = new int2(window.TopRightBoundary.C, window.TopRightBoundary.R);
                int2 sectorBot = FlowFieldUtilities.GetSector2D(windowBotLeft, sectorColAmount);
                int2 sectorTop = FlowFieldUtilities.GetSector2D(windowTopRight, sectorColAmount);
                int2 sectorBotStart = FlowFieldUtilities.GetSectorStartIndex(sectorBot, sectorColAmount);
                int2 sectorTopStart = FlowFieldUtilities.GetSectorStartIndex(sectorTop, sectorColAmount);
                int sectorBot1d = FlowFieldUtilities.To1D(sectorBot, sectorMatrixColAmount);
                int sectorTop1d = FlowFieldUtilities.To1D(sectorTop, sectorMatrixColAmount);
                int sectorBotCostStart = sectorBot1d * sectorTileAmount;
                int sectorTopCostStart = sectorTop1d * sectorTileAmount;

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
                    int2 botIndexGeneral2d = new int2(j, row1);
                    int2 topIndexGeneral2d = new int2(j, row2);
                    int botLocalIndex = FlowFieldUtilities.GetLocal1D(botIndexGeneral2d, sectorBotStart, sectorColAmount);
                    int topLocalIndex = FlowFieldUtilities.GetLocal1D(topIndexGeneral2d, sectorTopStart, sectorColAmount);

                    if (costs[sectorBotCostStart + botLocalIndex] != byte.MaxValue && costs[sectorTopCostStart + topLocalIndex] != byte.MaxValue)
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
                    if ((costs[sectorBotCostStart + botLocalIndex] == byte.MaxValue || costs[sectorTopCostStart + topLocalIndex] == byte.MaxValue) && !wasUnwalkable)
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
                int2 windowBotLeft = new int2(window.BottomLeftBoundary.C, window.BottomLeftBoundary.R);
                int2 windowTopRight = new int2(window.TopRightBoundary.C, window.TopRightBoundary.R);
                int2 sectorLeft = FlowFieldUtilities.GetSector2D(windowBotLeft, sectorColAmount);
                int2 sectorRight = FlowFieldUtilities.GetSector2D(windowTopRight, sectorColAmount);
                int2 sectorLeftStart = FlowFieldUtilities.GetSectorStartIndex(sectorLeft, sectorColAmount);
                int2 sectorRightStart = FlowFieldUtilities.GetSectorStartIndex(sectorRight, sectorColAmount);
                int sectorLeft1d = FlowFieldUtilities.To1D(sectorLeft, sectorMatrixColAmount);
                int sectorRight1d = FlowFieldUtilities.To1D(sectorRight, sectorMatrixColAmount);
                int sectorLeftCostStart = sectorLeft1d * sectorTileAmount;
                int sectorRightCostStart = sectorRight1d * sectorTileAmount;

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
                    int2 leftIndexGeneral2d = new int2(col1, j);
                    int2 rightIndexGeneral2d = new int2(col2, j);
                    int leftLocalIndex = FlowFieldUtilities.GetLocal1D(leftIndexGeneral2d, sectorLeftStart, sectorColAmount);
                    int rightLocalIndex = FlowFieldUtilities.GetLocal1D(rightIndexGeneral2d, sectorRightStart, sectorColAmount);
                    if (costs[sectorLeftCostStart + leftLocalIndex] != byte.MaxValue && costs[sectorRightCostStart + rightLocalIndex] != byte.MaxValue)
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
                    if ((costs[sectorLeftCostStart + leftLocalIndex] == byte.MaxValue || costs[sectorRightCostStart + rightLocalIndex] == byte.MaxValue) && !wasUnwalkable)
                    {
                        portalNodes[porPtr + portalCount] = GetPortalNodeBetween(bound1, bound2, porPtr, portalCount, i, false);
                        portalCount++;
                        wasUnwalkable = true;
                    }
                }
                if (!wasUnwalkable)
                {
                    portalNodes[porPtr + portalCount] = GetPortalNodeBetween(bound1, bound2, porPtr, portalCount, i, false);
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
        //DATA
        NativeQueue<int> integrationQueue = new NativeQueue<int>(Allocator.Temp);
        int sectorMatrixColAmount = SectorMatrixColAmount;
        int sectorColAmount = SectorColAmount;
        UnsafeList<PortalNode> portalNodes = PortalNodes;
        UnsafeList<SectorNode> sectorNodes = SectorNodes;
        NativeArray<PortalToPortal> porPtrs = PorToPorPtrs;
        NativeArray<WindowNode> windowNodes = WindowNodes;
        NativeArray<int> secToWinPtrs = SecToWinPtrs;
        int fieldColAmount = FieldColAmount;

        //CODE

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
                NativeArray<AStarTile> integratedCosts = GetIntegratedCostsFor(i, new int2(sourceIndex.C, sourceIndex.R), integrationQueue);
                CalculatePortalBounds(0, j);
                CalculatePortalBounds(j + 1, portalIndicies.Length);

                void CalculatePortalBounds(int fromInclusive, int toExclusive)
                {
                    for (int k = fromInclusive; k < toExclusive; k++)
                    {
                        PortalNode targetPortalNode = portalNodes[portalIndicies[k]];
                        byte pickedTargetPortalNumber = portalDeterminationArray[k];
                        Index2 targetIndex = pickedTargetPortalNumber == 1 ? targetPortalNode.Portal1.Index : targetPortalNode.Portal2.Index;
                        LocalIndex1d targetLocal = FlowFieldUtilities.GetLocal1D(new int2(targetIndex.C, targetIndex.R), sectorColAmount, sectorMatrixColAmount);

                        float cost = integratedCosts[targetLocal.index].IntegratedCost;

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

        //HELPERS

        NativeArray<byte> GetPortalDeterminationArrayFor(NativeArray<int> portalIndicies, int sectorIndex)
        {
            NativeArray<byte> determinationArray = new NativeArray<byte>(portalIndicies.Length, Allocator.Temp);
            for(int i = 0; i < determinationArray.Length; i++)
            {
                Portal portal1 = portalNodes[portalIndicies[i]].Portal1;
                Index2 sectorSpaceIndex = new Index2(portal1.Index.R / sectorColAmount, portal1.Index.C / sectorColAmount);
                determinationArray[i] = sectorIndex == sectorSpaceIndex.R * sectorMatrixColAmount + sectorSpaceIndex.C ? (byte) 1 : (byte) 2;
            }
            return determinationArray;
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
                portalIndexCount += windowNodes[secToWinPtrs[secToWinPtr + i]].PorCnt;
            }
            portalIndicies = new NativeArray<int>(portalIndexCount, Allocator.Temp);

            //get portals
            int portalIndiciesIterable = 0;
            for (int i = 0; i < secToWinCnt; i++)
            {
                int windowPorPtr = windowNodes[secToWinPtrs[secToWinPtr + i]].PorPtr;
                int windowPorCnt = windowNodes[secToWinPtrs[secToWinPtr + i]].PorCnt;
                for (int j = 0; j < windowPorCnt; j++)
                {
                    portalIndicies[portalIndiciesIterable++] = windowPorPtr + j;
                }
            }
            return portalIndicies;
        }
    }
    NativeArray<AStarTile> GetIntegratedCostsFor(int sectorIndex, int2 target, NativeQueue<int> integrationQueue)
    {
        //DATA
        int fieldColAmount = FieldColAmount;
        int fieldRowAmount = FieldRowAmount;
        int sectorColAmount = SectorColAmount;
        int sectorTileAmount = SectorTileAmount;
        NativeSlice<byte> costs = new NativeSlice<byte>(Costs, sectorIndex * SectorTileAmount, SectorTileAmount);
        NativeArray<AStarTile> integratedCosts = IntegratedCosts;
        integrationQueue.Clear();

        /////////////LOOKUP TABLE/////////////////
        //////////////////////////////////////////
        int nLocal1d;
        int eLocal1d;
        int sLocal1d;
        int wLocal1d;
        int neLocal1d;
        int seLocal1d;
        int swLocal1d;
        int nwLocal1d;
        //////////////////////////////////////////

        //CODE
        Reset();
        LocalIndex1d targetLocal = FlowFieldUtilities.GetLocal1D(target, SectorColAmount, SectorMatrixColAmount);
        int targetLocal1d = targetLocal.index;
        AStarTile targetTile = integratedCosts[targetLocal1d];
        targetTile.IntegratedCost = 0f;
        targetTile.Enqueued = true;
        integratedCosts[targetLocal1d] = targetTile;
        SetLookupTable(targetLocal1d);
        Enqueue();
        while (!integrationQueue.IsEmpty())
        {
            int index = integrationQueue.Dequeue();
            AStarTile tile = integratedCosts[index];
            SetLookupTable(index);
            tile.IntegratedCost = GetCost();
            integratedCosts[index] = tile;
            Enqueue();
        }
        return integratedCosts;

        //HELPERS
        void SetLookupTable(int curLocal1d)
        {
            nLocal1d = curLocal1d + sectorColAmount;
            eLocal1d = curLocal1d + 1;
            sLocal1d = curLocal1d - sectorColAmount;
            wLocal1d = curLocal1d - 1;
            neLocal1d = nLocal1d + 1;
            seLocal1d = sLocal1d + 1;
            swLocal1d = sLocal1d - 1;
            nwLocal1d = nLocal1d - 1;

            bool nLocalOverflow = nLocal1d >= sectorTileAmount;
            bool eLocalOverflow = (eLocal1d % sectorColAmount) == 0;
            bool sLocalOverflow = sLocal1d < 0;
            bool wLocalOverflow = (curLocal1d % sectorColAmount) == 0;

            nLocal1d = math.select(nLocal1d, curLocal1d, nLocalOverflow);
            eLocal1d = math.select(eLocal1d, curLocal1d, eLocalOverflow);
            sLocal1d = math.select(sLocal1d, curLocal1d, sLocalOverflow);
            wLocal1d = math.select(wLocal1d, curLocal1d, wLocalOverflow);
            neLocal1d = math.select(neLocal1d, curLocal1d, nLocalOverflow || eLocalOverflow);
            seLocal1d = math.select(seLocal1d, curLocal1d, sLocalOverflow || eLocalOverflow);
            swLocal1d = math.select(swLocal1d, curLocal1d, sLocalOverflow || wLocalOverflow);
            nwLocal1d = math.select(nwLocal1d, curLocal1d, nLocalOverflow || wLocalOverflow);
        }
        void Reset()
        {
            for(int i = 0; i < integratedCosts.Length; i++)
            {
                if (costs[i] == byte.MaxValue)
                {
                    integratedCosts[i] = new AStarTile(float.MaxValue, true);
                    continue;
                }
                integratedCosts[i] = new AStarTile(float.MaxValue, false);
            }
        }
        void Enqueue()
        {
            if (!integratedCosts[nLocal1d].Enqueued)
            {
                integrationQueue.Enqueue(nLocal1d);
                AStarTile tile = integratedCosts[nLocal1d];
                tile.Enqueued = true;
                integratedCosts[nLocal1d] = tile;
            }
            if (!integratedCosts[eLocal1d].Enqueued)
            {
                integrationQueue.Enqueue(eLocal1d);
                AStarTile tile = integratedCosts[eLocal1d];
                tile.Enqueued = true;
                integratedCosts[eLocal1d] = tile;
            }
            if (!integratedCosts[sLocal1d].Enqueued)
            {
                integrationQueue.Enqueue(sLocal1d);
                AStarTile tile = integratedCosts[sLocal1d];
                tile.Enqueued = true;
                integratedCosts[sLocal1d] = tile;
            }
            if (!integratedCosts[wLocal1d].Enqueued)
            {
                integrationQueue.Enqueue(wLocal1d);
                AStarTile tile = integratedCosts[wLocal1d];
                tile.Enqueued = true;
                integratedCosts[wLocal1d] = tile;
            }
        }
        float GetCost()
        {
            float costToReturn = float.MaxValue;
            float nCost = integratedCosts[nLocal1d].IntegratedCost + 1f;
            float neCost = integratedCosts[neLocal1d].IntegratedCost + 1.4f;
            float eCost = integratedCosts[eLocal1d].IntegratedCost + 1f;
            float seCost = integratedCosts[seLocal1d].IntegratedCost + 1.4f;
            float sCost = integratedCosts[sLocal1d].IntegratedCost + 1f;
            float swCost = integratedCosts[swLocal1d].IntegratedCost + 1.4f;
            float wCost = integratedCosts[wLocal1d].IntegratedCost + 1f;
            float nwCost = integratedCosts[nwLocal1d].IntegratedCost + 1.4f;
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
}
