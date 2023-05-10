using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.VisualScripting;

//Here, some vanilla procedural porgramming
[BurstCompile]
public struct CostFieldEditJob : IJob
{
    public Index2 Bound1;
    public Index2 Bound2;
    public NativeArray<SectorNode> SectorNodes;
    public NativeArray<int> SecToWinPtrs;
    public NativeArray<WindowNode> WindowNodes;
    public NativeArray<int> WinToSecPtrs;
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

    public NativeArray<WindowPair> debugArray;
    public NativeArray<int> windowCount;

    public void Execute()
    {
        int botLeft = Bound1.R * _fieldTileAmount + Bound1.C;
        int topRight = Bound2.R * _fieldTileAmount + Bound1.C;
        NativeArray<SectorPair> sectorsBetweenBounds = GetSectorsBetweenBounds();
        NativeArray<WindowPair> windowsBetweenBounds = GetWindowsBetweenBounds(sectorsBetweenBounds);
    }
    NativeArray<SectorPair> GetSectorsBetweenBounds()
    {
        int bottomLeftRow = Bound1.R / _sectorTileAmount;
        int bottomLeftCol = Bound1.C / _sectorTileAmount;
        int upperRightRow = Bound2.R / _sectorTileAmount;
        int upperRightCol = Bound2.C / _sectorTileAmount;

        int bottomLeft = bottomLeftRow * _sectorMatrixSize + bottomLeftCol;
        int upperRight = upperRightRow * _sectorMatrixSize + upperRightCol;

        bool isSectorOnTop = upperRight / _sectorMatrixSize == _sectorMatrixSize - 1;
        bool isSectorOnBot = bottomLeft - _sectorMatrixSize < 0;
        bool isSectorOnRight = (upperRight + 1) % _sectorMatrixSize == 0;
        bool isSectorOnLeft = bottomLeft % _sectorMatrixSize == 0;

        bool doesIntersectLowerSectors = Bound1.R % _sectorTileAmount == 0;
        bool doesIntersectUpperSectors = (Bound2.R + 1) % _sectorTileAmount == 0;
        bool doesIntersectLeftSectors = Bound1.C % _sectorTileAmount == 0;
        bool doesIntersectRightSectors = (Bound2.C + 1) % _sectorTileAmount == 0;

        int sectorRowCount = upperRightRow - bottomLeftRow + 1;
        int sectorColCount = upperRightCol - bottomLeftCol + 1;

        int sectorAmount = GetSectorAmount();
        NativeArray <SectorPair> sectorsToReturn = new NativeArray<SectorPair>(sectorAmount, Allocator.Temp);

        int sectorsToReturnIterable = 0;
        for(int r = bottomLeft; r < bottomLeft + sectorRowCount * _sectorMatrixSize; r += _sectorMatrixSize)
        {
            for(int i = r; i < r + sectorColCount; i++)
            {
                sectorsToReturn[sectorsToReturnIterable++] = new SectorPair(SectorNodes[i], i);
            }
        }
        if (!isSectorOnTop && doesIntersectUpperSectors)
        {
            for(int i = upperRight + _sectorMatrixSize; i > upperRight + _sectorMatrixSize - sectorColCount; i--)
            {
                sectorsToReturn[sectorsToReturnIterable++] = new SectorPair(SectorNodes[i], i);
            }
        }
        if (!isSectorOnBot && doesIntersectLowerSectors)
        {
            for(int i = bottomLeft - _sectorMatrixSize; i < bottomLeft - _sectorMatrixSize + sectorColCount; i++)
            {
                sectorsToReturn[sectorsToReturnIterable++] = new SectorPair(SectorNodes[i], i);
            }
        }
        if (!isSectorOnRight && doesIntersectRightSectors)
        {
            for(int i = upperRight + 1; i > upperRight + 1 - sectorRowCount * _sectorMatrixSize; i -= _sectorMatrixSize)
            {
                sectorsToReturn[sectorsToReturnIterable++] = new SectorPair(SectorNodes[i], i);
            }
        }
        if (!isSectorOnLeft && doesIntersectLeftSectors)
        {
            for(int i = bottomLeft - 1; i < bottomLeft - 1 + sectorRowCount * _sectorMatrixSize; i += _sectorMatrixSize)
            {
                sectorsToReturn[sectorsToReturnIterable++] = new SectorPair(SectorNodes[i], i);
            }
        }

        return sectorsToReturn;

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
    NativeArray<WindowPair> GetWindowsBetweenBounds(NativeArray<SectorPair> helperSectors)
    {
        int boundLeftC = Bound1.C;
        int boundRightC = Bound2.C;
        int boundBotR = Bound1.R;
        int boundTopR = Bound2.R;
        int horizontalSize = boundRightC - boundLeftC;
        int verticalSize = boundTopR - boundBotR;

        NativeArray<WindowPair> windowPairs = new NativeArray<WindowPair>(2 + helperSectors.Length * 2, Allocator.Temp);
        int windowPairsIterable = 0;
        for(int i = 0; i < helperSectors.Length; i++)
        {
            int secToWinPtr = helperSectors[i].Data.SecToWinPtr;
            int secToWinCnt = helperSectors[i].Data.SecToWinCnt;
            for(int j = secToWinPtr; j < secToWinPtr + secToWinCnt; j++)
            {
                Window window = WindowNodes[SecToWinPtrs[j]].Window;
                if (BoundsCollideWith(window))
                {
                    WindowPair windowPair = new WindowPair(WindowNodes[SecToWinPtrs[j]], SecToWinPtrs[j]);
                    if(ArrayContains(windowPairs, windowPair.Index)) { continue; }
                    windowPairs[windowPairsIterable++] = windowPair;   
                }
            }
        }
        return windowPairs.GetSubArray(0, windowPairsIterable);

        bool BoundsCollideWith(Window window)
        {
            int rightDistance = boundLeftC - window.TopRightBoundary.C;
            int leftDistance = window.BottomLeftBoundary.C - boundRightC;
            int topDitance = boundBotR - window.TopRightBoundary.R;
            int botDistance = window.BottomLeftBoundary.R - boundTopR;
            if(rightDistance > 0) { return false; }
            if(leftDistance > 0) { return false; }
            if(topDitance > 0) { return false; }
            if(botDistance > 0) { return false; }
            return true;
        }
        bool ArrayContains(NativeArray<WindowPair> windowPairs, int windowIndes)
        {
            for(int i = 0; i < windowPairs.Length; i++)
            {
                if (windowPairs[i].Index == windowIndes)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
public struct SectorPair
{
    public int Index;
    public SectorNode Data;

    public SectorPair(SectorNode data, int index)
    {
        Index = index;
        Data = data;
    }
}
public struct WindowPair
{
    public int Index;
    public WindowNode Data;

    public WindowPair(WindowNode data, int index)
    {
        Index = index;
        Data = data;
    }
}