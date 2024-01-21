using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
internal struct CostFieldEditJob : IJob
{
    internal int Offset;
    internal NativeList<CostEdit> NewCostEdits;
    internal NativeArray<SectorNode> SectorNodes;
    internal NativeArray<int> SecToWinPtrs;
    internal NativeArray<WindowNode> WindowNodes;
    internal NativeArray<int> WinToSecPtrs;
    internal NativeArray<PortalNode> PortalNodes;
    internal NativeArray<PortalToPortal> PorPtrs;
    internal NativeArray<byte> Costs;
    internal NativeArray<byte> BaseCosts;
    internal NativeArray<uint> CostStamps;
    internal int FieldColAmount;
    internal int FieldRowAmount;
    internal int SectorColAmount;
    internal int SectorRowAmount;
    internal int SectorTileAmount;
    internal int SectorMatrixColAmount;
    internal int SectorMatrixRowAmount;
    internal int PortalPerWindow;
    internal NativeArray<AStarTile> IntegratedCosts;
    internal NativeList<int> EditedSectorIndicies;
    internal NativeList<int> EditedWindowIndicies;
    internal NativeBitArray EditedWindowMarks;
    internal NativeArray<IslandData> Islands;
    internal NativeArray<UnsafeList<int>> IslandFields;
    internal SectorBitArray EditedSectorBits;

    public void Execute()
    {
        EditedSectorBits.Clear();
        EditedWindowIndicies.Clear();
        EditedWindowMarks.Clear();
        EditedSectorIndicies.Clear();

        //GIVE OFFSET TO OBSTACLE REQUESTS
        for(int i = 0; i < NewCostEdits.Length; i++)
        {
            CostEdit edit = NewCostEdits[i];
            int2 newBotLeft = edit.BotLeftBound + new int2(-Offset, -Offset);
            int2 newTopRight = edit.TopRightBound + new int2(Offset, Offset);

            newBotLeft.x = math.select(newBotLeft.x, 0, newBotLeft.x < 0);
            newBotLeft.y = math.select(newBotLeft.y, 0, newBotLeft.y < 0);
            newTopRight.x = math.select(newTopRight.x, FieldColAmount - 1, newTopRight.x >= FieldColAmount);
            newTopRight.y = math.select(newTopRight.y, FieldRowAmount - 1, newTopRight.y >= FieldRowAmount);
            NewCostEdits[i] = new CostEdit()
            {
                TopRightBound = newTopRight,
                BotLeftBound = newBotLeft,
                EditType = edit.EditType,
            };
        }

        ApplyCostUpdate();
        SetSectorsBetweenBounds();
        MarkPortalIslansOfEditedSectorsDirty();
        ResetConnectionsIn();
        RecalcualatePortalsAt();
        RecalculatePortalConnectionsAt();
    }
    void MarkPortalIslansOfEditedSectorsDirty()
    {
        for (int i = 0; i < EditedSectorIndicies.Length; i++)
        {
            int sectorIndex = EditedSectorIndicies[i];
            SectorNode sector = SectorNodes[sectorIndex];

            if (sector.IsIslandValid())
            {
                PortalNode islandPortal = PortalNodes[sector.SectorIslandPortalIndex];
                Islands[islandPortal.IslandIndex] = IslandData.Dirty;
            }
            else if (sector.IsIslandField)
            {
                UnsafeList<int> islandField = IslandFields[sectorIndex];
                for (int j = 0; j < islandField.Length; j++)
                {
                    int islandIndex = islandField[j];
                    if (islandIndex == int.MaxValue) { continue; }
                    if(islandIndex < 0)
                    {
                        Islands[-islandIndex] = IslandData.Dirty;
                    }
                    else
                    {
                        Islands[PortalNodes[islandIndex].IslandIndex] = IslandData.Dirty;

                    }
                }
                islandField.Clear();
                IslandFields[sectorIndex] = islandField;
            }
            int winPtrStart = sector.SecToWinPtr;
            int winPtrLen = sector.SecToWinCnt;
            for (int j = winPtrStart; j < winPtrStart + winPtrLen; j++)
            {
                int windowIndex = SecToWinPtrs[j];
                WindowNode window = WindowNodes[windowIndex];
                int porStart = window.PorPtr;
                int porCnt = window.PorCnt;

                for (int k = porStart; k < porStart + porCnt; k++)
                {
                    PortalNode portal = PortalNodes[k];
                    Islands[portal.IslandIndex] = IslandData.Dirty;
                }
            }
            sector.IsIslandField = false;
            sector.SectorIslandPortalIndex = -1;
            SectorNodes[sectorIndex] = sector;
        }
    }
    void ApplyCostUpdate()
    {
        int sectorColAmount = SectorColAmount;
        int sectorMatrixColAmount = SectorMatrixColAmount;
        int sectorTileAmount = SectorColAmount * SectorColAmount;

        for (int i = 0; i < NewCostEdits.Length; i++)
        {
            CostEdit edit = NewCostEdits[i];
            Index2 botLeft = new Index2(edit.BotLeftBound.y, edit.BotLeftBound.x);
            Index2 topRight = new Index2(edit.TopRightBound.y, edit.TopRightBound.x);
            if (botLeft.R == 0) { botLeft.R += 1; }
            if (botLeft.C == 0) { botLeft.C += 1; }
            if (topRight.R == FieldRowAmount - 1) { topRight.R -= 1; }
            if (topRight.C == FieldColAmount - 1) { topRight.C -= 1; }

            int eastCount = topRight.C - botLeft.C;
            int northCount = topRight.R - botLeft.R;
            LocalIndex1d localBotLeft = GetLocalIndex(botLeft);
            LocalIndex1d startLocal1d = localBotLeft;
            LocalIndex1d curLocalIndex = localBotLeft;
            NativeSlice<byte> costSector;
            NativeSlice<uint> costStampSector;
            NativeSlice<byte> baseCostSector;
            if(edit.EditType == CostEditType.Set)
            {
                for (int index = 0; index <= northCount; index++)
                {
                    for (int j = 0; j <= eastCount; j++)
                    {
                        costSector = new NativeSlice<byte>(Costs, curLocalIndex.sector * sectorTileAmount, sectorTileAmount);
                        costStampSector = new NativeSlice<uint>(CostStamps, curLocalIndex.sector * sectorTileAmount, sectorTileAmount);
                        costStampSector[curLocalIndex.index] += 1;
                        costSector[curLocalIndex.index] = byte.MaxValue;
                        curLocalIndex = GetEast(curLocalIndex);
                    }
                    startLocal1d = GetNorth(startLocal1d);
                    curLocalIndex = startLocal1d;
                }
            }
            else if(edit.EditType == CostEditType.Clear)
            {

                for (int index = 0; index <= northCount; index++)
                {
                    for (int j = 0; j <= eastCount; j++)
                    {
                        costSector = new NativeSlice<byte>(Costs, curLocalIndex.sector * sectorTileAmount, sectorTileAmount);
                        costStampSector = new NativeSlice<uint>(CostStamps, curLocalIndex.sector * sectorTileAmount, sectorTileAmount);
                        baseCostSector = new NativeSlice<byte>(BaseCosts, curLocalIndex.sector * sectorTileAmount, sectorTileAmount);

                        uint curStamp = costStampSector[curLocalIndex.index] - 1;
                        costStampSector[curLocalIndex.index] = curStamp;
                        if(curStamp == 0)
                        {
                            costSector[curLocalIndex.index] = baseCostSector[curLocalIndex.index];
                        }
                        curLocalIndex = GetEast(curLocalIndex);
                    }
                    startLocal1d = GetNorth(startLocal1d);
                    curLocalIndex = startLocal1d;
                }
            }
        }
        

        

        LocalIndex1d GetLocalIndex(Index2 index)
        {
            int2 general2d = new int2(index.C, index.R);
            int2 sector2d = general2d / sectorColAmount;
            int sector1d = sector2d.y * sectorMatrixColAmount + sector2d.x;
            int2 sectorStart2d = sector2d * sectorColAmount;
            int2 local2d = general2d - sectorStart2d;
            int local1d = local2d.y * sectorColAmount + local2d.x;
            return new LocalIndex1d(local1d, sector1d);
        }
        LocalIndex1d GetEast(LocalIndex1d local)
        {
            int eLocal1d = local.index + 1;
            bool eLocalOverflow = (eLocal1d % sectorColAmount) == 0;
            int eSector1d = math.select(local.sector, local.sector + 1, eLocalOverflow);
            eLocal1d = math.select(eLocal1d, local.index - sectorColAmount + 1, eLocalOverflow);
            return new LocalIndex1d(eLocal1d, eSector1d);
        }
        LocalIndex1d GetNorth(LocalIndex1d local)
        {
            int nLocal1d = local.index + sectorColAmount;
            bool nLocalOverflow = nLocal1d >= sectorTileAmount;
            int nSector1d = math.select(local.sector, local.sector + sectorMatrixColAmount, nLocalOverflow);
            nLocal1d = math.select(nLocal1d, local.index - (sectorColAmount * sectorColAmount - sectorColAmount), nLocalOverflow);
            return new LocalIndex1d(nLocal1d, nSector1d);
        }
    }
    void SetSectorsBetweenBounds()
    {
        for(int index = 0; index < NewCostEdits.Length; index++)
        {
            CostEdit edit = NewCostEdits[index];

            Index2 botLeft = new Index2(edit.BotLeftBound.y, edit.BotLeftBound.x);
            Index2 topRight = new Index2(edit.TopRightBound.y, edit.TopRightBound.x);
            int bottomLeftRow = botLeft.R / SectorColAmount;
            int bottomLeftCol = botLeft.C / SectorColAmount;
            int upperRightRow = topRight.R / SectorColAmount;
            int upperRightCol = topRight.C / SectorColAmount;

            int bottomLeft = bottomLeftRow * SectorMatrixColAmount + bottomLeftCol;
            int upperRight = upperRightRow * SectorMatrixColAmount + upperRightCol;

            bool isSectorOnTop = upperRight / SectorMatrixColAmount == SectorMatrixRowAmount - 1;
            bool isSectorOnBot = bottomLeft - SectorMatrixColAmount < 0;
            bool isSectorOnRight = (upperRight + 1) % SectorMatrixColAmount == 0;
            bool isSectorOnLeft = bottomLeft % SectorMatrixColAmount == 0;

            bool doesIntersectLowerSectors = botLeft.R % SectorColAmount == 0;
            bool doesIntersectUpperSectors = (topRight.R + 1) % SectorColAmount == 0;
            bool doesIntersectLeftSectors = botLeft.C % SectorColAmount == 0;
            bool doesIntersectRightSectors = (topRight.C + 1) % SectorColAmount == 0;

            int sectorRowCount = upperRightRow - bottomLeftRow + 1;
            int sectorColCount = upperRightCol - bottomLeftCol + 1;

            for (int r = bottomLeft; r < bottomLeft + sectorRowCount * SectorMatrixColAmount; r += SectorMatrixColAmount)
            {
                for (int i = r; i < r + sectorColCount; i++)
                {
                    if (EditedSectorBits.HasBit(i)) { continue; }
                    EditedSectorBits.SetSector(i);
                    EditedSectorIndicies.Add(i);
                    AddToEditedWindowsIfBoundsIntersect(edit.BotLeftBound, edit.TopRightBound, i);
                }
            }
            if (!isSectorOnTop && doesIntersectUpperSectors)
            {
                for (int i = upperRight + SectorMatrixColAmount; i > upperRight + SectorMatrixColAmount - sectorColCount; i--)
                {
                    if (EditedSectorBits.HasBit(i)) { continue; }
                    EditedSectorBits.SetSector(i);
                    EditedSectorIndicies.Add(i);
                    AddToEditedWindowsIfBoundsIntersect(edit.BotLeftBound, edit.TopRightBound, i);
                }
            }
            if (!isSectorOnBot && doesIntersectLowerSectors)
            {
                for (int i = bottomLeft - SectorMatrixColAmount; i < bottomLeft - SectorMatrixColAmount + sectorColCount; i++)
                {
                    if (EditedSectorBits.HasBit(i)) { continue; }
                    EditedSectorBits.SetSector(i);
                    EditedSectorIndicies.Add(i);
                    AddToEditedWindowsIfBoundsIntersect(edit.BotLeftBound, edit.TopRightBound, i);
                }
            }
            if (!isSectorOnRight && doesIntersectRightSectors)
            {
                for (int i = upperRight + 1; i > upperRight + 1 - sectorRowCount * SectorMatrixColAmount; i -= SectorMatrixColAmount)
                {
                    if (EditedSectorBits.HasBit(i)) { continue; }
                    EditedSectorBits.SetSector(i);
                    EditedSectorIndicies.Add(i);
                    AddToEditedWindowsIfBoundsIntersect(edit.BotLeftBound, edit.TopRightBound, i);
                }
            }
            if (!isSectorOnLeft && doesIntersectLeftSectors)
            {
                for (int i = bottomLeft - 1; i < bottomLeft - 1 + sectorRowCount * SectorMatrixColAmount; i += SectorMatrixColAmount)
                {
                    if (EditedSectorBits.HasBit(i)) { continue; }
                    EditedSectorBits.SetSector(i);
                    EditedSectorIndicies.Add(i);
                    AddToEditedWindowsIfBoundsIntersect(edit.BotLeftBound, edit.TopRightBound, i);
                }
            }
        }
    }
    internal void AddToEditedWindowsIfBoundsIntersect(int2 botLeft, int2 topRigth, int sectorIndex)
    {
        SectorNode sectorNode = SectorNodes[sectorIndex];
        int secToWinPtr = sectorNode.SecToWinPtr;
        int secToWinCnt = sectorNode.SecToWinCnt;
        for (int j = secToWinPtr; j < secToWinPtr + secToWinCnt; j++)
        {
            int windowIndex = SecToWinPtrs[j];
            Window window = WindowNodes[windowIndex].Window;
            if (BoundsCollideWith(window) && !EditedWindowMarks.IsSet(windowIndex))
            {
                EditedWindowMarks.Set(windowIndex, true);
                EditedWindowIndicies.Add(windowIndex);
            }
        }
        bool BoundsCollideWith(Window window)
        {
            int rightDistance = botLeft.x - window.TopRightBoundary.C;
            int leftDistance = window.BottomLeftBoundary.C - topRigth.x;
            int topDitance = botLeft.y - window.TopRightBoundary.R;
            int botDistance = window.BottomLeftBoundary.R - topRigth.y;
            return rightDistance <=0 && leftDistance <=0 && topDitance <=0 && botDistance <=0;
        }
    }
    void ResetConnectionsIn()
    {
        for (int i = 0; i < EditedSectorIndicies.Length; i++)
        {
            int pickedSectorIndex = EditedSectorIndicies[i];
            SectorNode pickedSectorNode = SectorNodes[pickedSectorIndex];
            int pickedSecToWinPtr = pickedSectorNode.SecToWinPtr;
            for (int j = 0; j < pickedSectorNode.SecToWinCnt; j++)
            {
                WindowNode pickedWindowNode = WindowNodes[SecToWinPtrs[pickedSecToWinPtr + j]];
                int pickedWinToPorPtr = pickedWindowNode.PorPtr;
                for (int k = 0; k < pickedWindowNode.PorCnt; k++)
                {
                    PortalNode pickedPortalNode = PortalNodes[pickedWinToPorPtr + k];
                    int portal1SectorspaceFlatIndex = (pickedPortalNode.Portal1.Index.R / SectorColAmount) * SectorMatrixColAmount + (pickedPortalNode.Portal1.Index.C / SectorColAmount);
                    if (portal1SectorspaceFlatIndex == pickedSectorIndex)
                    {
                        pickedPortalNode.Portal1.PorToPorCnt = 0;
                    }
                    else
                    {
                        pickedPortalNode.Portal2.PorToPorCnt = 0;
                    }
                    PortalNodes[pickedWinToPorPtr + k] = pickedPortalNode;
                }
            }
        }
    }
    void RecalcualatePortalsAt()
    {
        NativeArray<int> windowIndicies = EditedWindowIndicies;
        int porToPorCnt = PortalPerWindow * 8 - 2;
        int fieldColAmount = FieldColAmount;
        int sectorColAmount = SectorColAmount;
        int sectorRowAmount = SectorRowAmount;
        int sectorTileAmount = SectorTileAmount;
        int sectorMatrixColAmount = SectorMatrixColAmount;

        NativeArray<byte> costs = Costs;
        NativeArray<PortalNode> portalNodes = PortalNodes;

        for (int i = 0; i < windowIndicies.Length; i++)
        {
            int windowIndex = windowIndicies[i];
            WindowNode windowNode = WindowNodes[windowIndex];
            Window window = windowNode.Window;
            if (window.IsHorizontal())
            {
                ConfigureForHorizontal();
                WindowNodes[windowIndex] = windowNode;
            }
            else
            {
                ConfigureForVertical();
                WindowNodes[windowIndex] = windowNode;
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

                int porPtr = windowNode.PorPtr;
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
                        portalNodes[porPtr + portalCount] = GetPortalNodeBetween(bound1, bound2, porPtr, portalCount, windowIndicies[i], true);
                        portalCount++;
                        wasUnwalkable = true;
                    }
                }
                if (!wasUnwalkable)
                {
                    portalNodes[porPtr + portalCount] = GetPortalNodeBetween(bound1, bound2, porPtr, portalCount, windowIndicies[i], true);
                    portalCount++;
                }
                windowNode.PorCnt = portalCount;
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

                int porPtr = windowNode.PorPtr;
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
                        portalNodes[porPtr + portalCount] = GetPortalNodeBetween(bound1, bound2, porPtr, portalCount, windowIndicies[i], false);
                        portalCount++;
                        wasUnwalkable = true;
                    }
                }
                if (!wasUnwalkable)
                {
                    portalNodes[porPtr + portalCount] = GetPortalNodeBetween(bound1, bound2, porPtr, portalCount, windowIndicies[i], false);
                    portalCount++;
                }
                windowNode.PorCnt = portalCount;
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
    void RecalculatePortalConnectionsAt()
    {
        NativeArray<int> sectorIndicies = EditedSectorIndicies;
        //data
        NativeQueue<int> integrationQueue = new NativeQueue<int>(Allocator.Temp);
        int sectorMatrixColAmount = SectorMatrixColAmount;
        int sectorColAmount = SectorColAmount;
        int sectorTileAmount = SectorTileAmount;
        NativeArray<WindowNode> windowNodes = WindowNodes;
        NativeArray<int> secToWinPtrs = SecToWinPtrs;
        NativeArray<PortalNode> portalNodes = PortalNodes;
        NativeArray<PortalToPortal> porPtrs = PorPtrs;
        int fieldColAmount = FieldColAmount;

        //function
        for(int i =0; i < sectorIndicies.Length; i++)
        {
            int sectorIndex = sectorIndicies[i];
            SectorNode pickedSectorNode = SectorNodes[sectorIndex];
            Sector pickedSector = pickedSectorNode.Sector;
            NativeArray<int> portalNodeIndicies = GetPortalNodeIndicies(pickedSectorNode);
            NativeArray<byte> portalDeterminationArray = GetPortalDeterminationArrayFor(portalNodeIndicies, sectorIndex);
            for (int j = 0; j < portalNodeIndicies.Length; j++)
            {
                //for each portal, set it "target" and calculate distances of others
                PortalNode sourcePortalNode = PortalNodes[portalNodeIndicies[j]];
                byte sourcePortalDetermination = portalDeterminationArray[j];
                Index2 sourceIndex = sourcePortalDetermination == 1 ? sourcePortalNode.Portal1.Index : sourcePortalNode.Portal2.Index;
                NativeArray<AStarTile> integratedCosts = GetIntegratedCostsFor(sectorIndex, new int2(sourceIndex.C, sourceIndex.R), integrationQueue);
                CalculatePortalConnections(0, j);
                CalculatePortalConnections(j+1, portalNodeIndicies.Length);
                PortalNodes[portalNodeIndicies[j]] = sourcePortalNode;
                void CalculatePortalConnections(int fromInclusive, int toExclusive)
                {
                    for (int k = fromInclusive; k < toExclusive; k++)
                    {
                        PortalNode targetPortalNode = portalNodes[portalNodeIndicies[k]];
                        byte pickedTargetPortalNumber = portalDeterminationArray[k];
                        Index2 targetIndex = pickedTargetPortalNumber == 1 ? targetPortalNode.Portal1.Index : targetPortalNode.Portal2.Index;
                        LocalIndex1d targetLocal = FlowFieldUtilities.GetLocal1D(new int2(targetIndex.C, targetIndex.R), sectorColAmount, sectorMatrixColAmount);
                        float cost = integratedCosts[targetLocal.index].IntegratedCost;

                        if (cost == float.MaxValue) { continue; }
                        if (sourcePortalDetermination == 1)
                        {
                            sourcePortalNode.Portal1.PorToPorCnt++;
                            porPtrs[sourcePortalNode.Portal1.PorToPorPtr + sourcePortalNode.Portal1.PorToPorCnt - 1] = new PortalToPortal(cost, portalNodeIndicies[k]);
                        }
                        else
                        {
                            sourcePortalNode.Portal2.PorToPorCnt++;
                            porPtrs[sourcePortalNode.Portal2.PorToPorPtr + sourcePortalNode.Portal2.PorToPorCnt - 1] = new PortalToPortal(cost, portalNodeIndicies[k]);
                        }
                    }
                }
            }
        }
        NativeArray<byte> GetPortalDeterminationArrayFor(NativeArray<int> portalNodeIndicies, int sectorIndex)
        {
            NativeArray<byte> determinationArray = new NativeArray<byte>(portalNodeIndicies.Length, Allocator.Temp);
            for (int i = 0; i < determinationArray.Length; i++)
            {
                Portal portal1 = portalNodes[portalNodeIndicies[i]].Portal1;
                Index2 sectorSpaceIndex = new Index2(portal1.Index.R / sectorColAmount, portal1.Index.C / sectorColAmount);
                determinationArray[i] = sectorIndex == sectorSpaceIndex.R * sectorMatrixColAmount + sectorSpaceIndex.C ? (byte)1 : (byte)2;
            }
            return determinationArray;
        }
        NativeArray<int> GetPortalNodeIndicies(SectorNode sectorNode)
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


    //A* Algorithm
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
            for (int i = 0; i < integratedCosts.Length; i++)
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