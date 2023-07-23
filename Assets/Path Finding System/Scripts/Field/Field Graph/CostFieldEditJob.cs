using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

[BurstCompile]
public struct CostFieldEditJob : IJob
{
    public byte NewCost;
    public BoundaryData Bounds;
    public NativeArray<SectorNode> SectorNodes;
    public NativeArray<int> SecToWinPtrs;
    public NativeArray<WindowNode> WindowNodes;
    public NativeArray<int> WinToSecPtrs;
    public NativeArray<PortalNode> PortalNodes;
    public NativeArray<PortalToPortal> PorPtrs;
    public NativeArray<UnsafeList<byte>> CostsL;
    public NativeArray<byte> CostsG;
    public int FieldColAmount;
    public int FieldRowAmount;
    public int SectorColAmount;
    public int SectorMatrixColAmount;
    public int SectorMatrixRowAmount;
    public int PortalPerWindow;
    public NativeArray<AStarTile> IntegratedCosts;
    public NativeQueue<int> AStarQueue;
    public NativeList<int> EditedSectorIndicies;


    int _sectorTileAmount;
    public void Execute()
    {
        _sectorTileAmount = SectorColAmount * SectorColAmount;
        ApplyCostUpdate();
        SetSectorsBetweenBounds();
        NativeArray<int> windowIndiciesBetweenBounds = GetWindowsBetweenBounds(EditedSectorIndicies);
        ResetConnectionsIn(EditedSectorIndicies);
        RecalcualatePortalsAt(windowIndiciesBetweenBounds);
        RecalculatePortalConnectionsAt(EditedSectorIndicies);
    }
    void ApplyCostUpdate()
    {
        int sectorColAmount = SectorColAmount;
        int sectorMatrixColAmount = SectorMatrixColAmount;
        //APPLY FOR GENERAL COST FIELD
        Index2 botLeft = Bounds.BottomLeft;
        Index2 topRight = Bounds.UpperRight;
        if (botLeft.R == 0) { botLeft.R += 1; }
        if (botLeft.C == 0) { botLeft.C += 1; }
        if (topRight.R == FieldRowAmount - 1) { topRight.R -= 1; }
        if(topRight.C == FieldColAmount - 1) { topRight.C -= 1; }

        int bound1Flat = botLeft.R * FieldColAmount + botLeft.C;
        int bound2Flat = topRight.R * FieldColAmount + topRight.C;
        int colDif = topRight.C - botLeft.C;

        for(int r = bound1Flat; r <= bound2Flat - colDif; r += FieldColAmount)
        {
            for(int i = r; i <= r + colDif; i++)
            {
                CostsG[i] = NewCost;
            }
        }

        //APPLY FOR LOCAL COST FIELD
        int eastCount = topRight.C - botLeft.C;
        int northCount = topRight.R - botLeft.R;
        int sectorTileAmount = SectorColAmount * SectorColAmount;
        LocalIndex1d localBotLeft = GetLocalIndex(botLeft);
        LocalIndex1d startLocal1d = localBotLeft;
        LocalIndex1d curLocalIndex = localBotLeft;
        UnsafeList<byte> costSector;
        for(int i = 0; i <= northCount; i++)
        {
            for(int j = 0; j <= eastCount; j++)
            {
                costSector = CostsL[curLocalIndex.sector];
                costSector[curLocalIndex.index] = NewCost;
                curLocalIndex = GetEast(curLocalIndex);
            }
            startLocal1d = GetNorth(startLocal1d);
            curLocalIndex = startLocal1d;
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
        Index2 botLeft = Bounds.BottomLeft;
        Index2 topRight = Bounds.UpperRight;
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
                EditedSectorIndicies.Add(i);
            }
        }
        if (!isSectorOnTop && doesIntersectUpperSectors)
        {
            for (int i = upperRight + SectorMatrixColAmount; i > upperRight + SectorMatrixColAmount - sectorColCount; i--)
            {
                EditedSectorIndicies.Add(i);
            }
        }
        if (!isSectorOnBot && doesIntersectLowerSectors)
        {
            for (int i = bottomLeft - SectorMatrixColAmount; i < bottomLeft - SectorMatrixColAmount + sectorColCount; i++)
            {
                EditedSectorIndicies.Add(i);
            }
        }
        if (!isSectorOnRight && doesIntersectRightSectors)
        {
            for (int i = upperRight + 1; i > upperRight + 1 - sectorRowCount * SectorMatrixColAmount; i -= SectorMatrixColAmount)
            {
                EditedSectorIndicies.Add(i);
            }
        }
        if (!isSectorOnLeft && doesIntersectLeftSectors)
        {
            for (int i = bottomLeft - 1; i < bottomLeft - 1 + sectorRowCount * SectorMatrixColAmount; i += SectorMatrixColAmount)
            {
                EditedSectorIndicies.Add(i);
            }
        }
        int GetSectorAmount()
        {
            int amount = sectorRowCount * sectorColCount;
            if (!isSectorOnTop && doesIntersectUpperSectors)
            {
                amount += sectorColCount;
            }
            if (!isSectorOnBot && doesIntersectLowerSectors)
            {
                amount += sectorColCount;
            }
            if (!isSectorOnRight && doesIntersectRightSectors)
            {
                amount += sectorRowCount;
            }
            if (!isSectorOnLeft && doesIntersectLeftSectors)
            {
                amount += sectorRowCount;
            }
            return amount;
        }
    }
    NativeArray<int> GetWindowsBetweenBounds(NativeArray<int> helperSectorIndicies)
    {
        Index2 botLeft = Bounds.BottomLeft;
        Index2 topRight = Bounds.UpperRight;
        int boundLeftC = botLeft.C;
        int boundRightC = topRight.C;
        int boundBotR = botLeft.R;
        int boundTopR = topRight.R;

        NativeArray<int> windows = new NativeArray<int>(2 + helperSectorIndicies.Length * 2, Allocator.Temp);
        int windowIterable = 0;
        for (int i = 0; i < helperSectorIndicies.Length; i++)
        {
            int secToWinPtr = SectorNodes[helperSectorIndicies[i]].SecToWinPtr;
            int secToWinCnt = SectorNodes[helperSectorIndicies[i]].SecToWinCnt;
            for (int j = secToWinPtr; j < secToWinPtr + secToWinCnt; j++)
            {
                int windowIndex = SecToWinPtrs[j];
                Window window = WindowNodes[windowIndex].Window;
                if (BoundsCollideWith(window))
                {
                    if (ArrayContains(windows, windowIterable, windowIndex)) { continue; }
                    windows[windowIterable++] = windowIndex;
                }
            }
        }
        return windows.GetSubArray(0, windowIterable);

        bool BoundsCollideWith(Window window)
        {
            int rightDistance = boundLeftC - window.TopRightBoundary.C;
            int leftDistance = window.BottomLeftBoundary.C - boundRightC;
            int topDitance = boundBotR - window.TopRightBoundary.R;
            int botDistance = window.BottomLeftBoundary.R - boundTopR;
            if (rightDistance > 0) { return false; }
            if (leftDistance > 0) { return false; }
            if (topDitance > 0) { return false; }
            if (botDistance > 0) { return false; }
            return true;
        }
        bool ArrayContains(NativeArray<int> windowPairs, int windowCount,int windowIndex)
        {
            for (int i = 0; i < windowCount; i++)
            {
                if (windowPairs[i] == windowIndex)
                {
                    return true;
                }
            }
            return false;
        }
    }
    void ResetConnectionsIn(NativeArray<int> sectorIndicies)
    {
        for (int i = 0; i < sectorIndicies.Length; i++)
        {
            int pickedSectorIndex = sectorIndicies[i];
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
    void RecalcualatePortalsAt(NativeArray<int> windowIndicies)
    {
        int porToPorCnt = PortalPerWindow * 8 - 2;
        int fieldColAmount = FieldColAmount;
        NativeArray<byte> costs = CostsG;
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
    void RecalculatePortalConnectionsAt(NativeArray<int> sectorIndicies)
    {
        //data
        int sectorMatrixColAmount = SectorMatrixColAmount;
        int sectorTileAmount = SectorColAmount;
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
                NativeArray<AStarTile> integratedCosts = GetIntegratedCostsFor(pickedSector, sourceIndex);
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
                        int targetIndexFlat = targetIndex.R * fieldColAmount + targetIndex.C;
                        float cost = integratedCosts[targetIndexFlat].IntegratedCost;

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
                Index2 sectorSpaceIndex = new Index2(portal1.Index.R / sectorTileAmount, portal1.Index.C / sectorTileAmount);
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
    NativeArray<AStarTile> GetIntegratedCostsFor(Sector sector, Index2 target)
    {
        //DATA
        int fieldColAmount = FieldColAmount;
        int fieldRowAmount = FieldRowAmount;
        NativeArray<byte> costs = CostsG;
        NativeArray<AStarTile> integratedCosts = IntegratedCosts;
        NativeQueue<int> aStarQueue = AStarQueue;

        /////////////LOOKUP TABLE/////////////////
        //////////////////////////////////////////
        int n;
        int e;
        int s;
        int w;
        int ne;
        int se;
        int sw;
        int nw;
        //////////////////////////////////////////

        //CODE
        Reset(sector);
        int targetIndex = Index2.ToIndex(target, FieldColAmount);
        AStarTile targetTile = integratedCosts[targetIndex];
        targetTile.IntegratedCost = 0f;
        targetTile.Enqueued = true;
        integratedCosts[targetIndex] = targetTile;
        SetLookupTable(targetIndex);
        Enqueue();
        while (!AStarQueue.IsEmpty())
        {
            int index = AStarQueue.Dequeue();
            AStarTile tile = integratedCosts[index];
            SetLookupTable(index);
            tile.IntegratedCost = GetCost();
            integratedCosts[index] = tile;
            Enqueue();
        }
        return integratedCosts;

        //HELPERS
        void SetLookupTable(int index)
        {
            n = index + fieldColAmount;
            e = index + 1;
            s = index - fieldColAmount;
            w = index - 1;
            ne = n + 1;
            se = s + 1;
            sw = s - 1;
            nw = n - 1;
        }
        void Reset(Sector sector)
        {
            Index2 lowerBound = sector.StartIndex;
            Index2 upperBound = new Index2(sector.StartIndex.R + sector.Size - 1, sector.StartIndex.C + sector.Size - 1);
            int lowerBoundIndex = Index2.ToIndex(lowerBound, fieldColAmount);
            int upperBoundIndex = Index2.ToIndex(upperBound, fieldColAmount);

            for (int r = lowerBoundIndex; r < lowerBoundIndex + sector.Size * fieldColAmount; r += fieldColAmount)
            {
                for (int i = r; i < r + sector.Size; i++)
                {
                    if (costs[i] == byte.MaxValue)
                    {
                        integratedCosts[i] = new AStarTile(float.MaxValue, true);
                        continue;
                    }
                    integratedCosts[i] = new AStarTile(float.MaxValue, false);
                }
            }
            SetEdgesUnwalkable(sector, lowerBoundIndex, upperBoundIndex);

            //HELPERS
            void SetEdgesUnwalkable(Sector sector, int lowerBoundIndex, int upperBoundIndex)
            {
                bool notOnBottom = !sector.IsOnBottom();
                bool notOnTop = !sector.IsOnTop(fieldRowAmount);
                bool notOnRight = !sector.IsOnRight(fieldColAmount);
                bool notOnLeft = !sector.IsOnLeft();
                if (notOnBottom)
                {
                    for (int i = lowerBoundIndex - fieldColAmount; i < (lowerBoundIndex - fieldColAmount) + sector.Size; i++)
                    {
                        integratedCosts[i] = new AStarTile(float.MaxValue, true);
                    }
                }
                if (notOnTop)
                {
                    for (int i = upperBoundIndex + fieldColAmount; i > upperBoundIndex + fieldColAmount - sector.Size; i--)
                    {
                        integratedCosts[i] = new AStarTile(float.MaxValue, true);
                    }
                }
                if (notOnRight)
                {
                    for (int i = upperBoundIndex + 1; i >= lowerBoundIndex + sector.Size; i -= fieldColAmount)
                    {
                        integratedCosts[i] = new AStarTile(float.MaxValue, true);
                    }
                }
                if (notOnLeft)
                {
                    for (int i = lowerBoundIndex - 1; i <= upperBoundIndex - sector.Size; i += fieldColAmount)
                    {
                        integratedCosts[i] = new AStarTile(float.MaxValue, true);
                    }
                }
                if (notOnRight && notOnBottom)
                {
                    integratedCosts[lowerBoundIndex + sector.Size - fieldColAmount] = new AStarTile(float.MaxValue, true);
                }
                if (notOnRight && notOnTop)
                {
                    integratedCosts[upperBoundIndex + fieldColAmount + 1] = new AStarTile(float.MaxValue, true);
                }
                if (notOnLeft && notOnBottom)
                {
                    integratedCosts[lowerBoundIndex - fieldColAmount - 1] = new AStarTile(float.MaxValue, true);
                }
                if (notOnLeft && notOnTop)
                {
                    integratedCosts[upperBoundIndex + fieldColAmount - sector.Size] = new AStarTile(float.MaxValue, true);
                }
            }
        }
        void Enqueue()
        {
            if (!integratedCosts[n].Enqueued)
            {
                aStarQueue.Enqueue(n);
                AStarTile tile = integratedCosts[n];
                tile.Enqueued = true;
                integratedCosts[n] = tile;
            }
            if (!integratedCosts[e].Enqueued)
            {
                aStarQueue.Enqueue(e);
                AStarTile tile = integratedCosts[e];
                tile.Enqueued = true;
                integratedCosts[e] = tile;
            }
            if (!integratedCosts[s].Enqueued)
            {
                aStarQueue.Enqueue(s);
                AStarTile tile = integratedCosts[s];
                tile.Enqueued = true;
                integratedCosts[s] = tile;
            }
            if (!integratedCosts[w].Enqueued)
            {
                aStarQueue.Enqueue(w);
                AStarTile tile = integratedCosts[w];
                tile.Enqueued = true;
                integratedCosts[w] = tile;
            }
        }
        float GetCost()
        {
            float costToReturn = float.MaxValue;
            float nCost = integratedCosts[n].IntegratedCost + 1f;
            float neCost = integratedCosts[ne].IntegratedCost + 1.4f;
            float eCost = integratedCosts[e].IntegratedCost + 1f;
            float seCost = integratedCosts[se].IntegratedCost + 1.4f;
            float sCost = integratedCosts[s].IntegratedCost + 1f;
            float swCost = integratedCosts[sw].IntegratedCost + 1.4f;
            float wCost = integratedCosts[w].IntegratedCost + 1f;
            float nwCost = integratedCosts[nw].IntegratedCost + 1.4f;
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