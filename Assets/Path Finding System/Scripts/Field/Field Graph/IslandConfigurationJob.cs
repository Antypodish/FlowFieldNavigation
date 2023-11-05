using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;

[BurstCompile]
public struct IslandConfigurationJob : IJob
{
    public int SectorTileAmount;
    public int SectorMatrixColAmount;
    public int SectorColAmount;
    [ReadOnly] public NativeArray<UnsafeList<byte>> CostsL;
    [ReadOnly] public NativeArray<PortalToPortal> PortalEdges;
    [ReadOnly] public NativeArray<WindowNode> WindowNodes;
    [ReadOnly] public NativeArray<int> SecToWinPtrs;
    public UnsafeList<SectorNode> SectorNodes;
    public UnsafeList<UnsafeList<int>> IslandFields;
    public UnsafeList<PortalNode> PortalNodes;
    public NativeList<IslandData> Islands;
    public void Execute()
    {
        //PREALLOCATIONS
        UnsafeStack<int> dfsStack = new UnsafeStack<int>(0);
        UnsafeList<int> islandCalculationMatrix = new UnsafeList<int>(SectorTileAmount, Allocator.Temp);
        islandCalculationMatrix.Length = SectorTileAmount;
        //CALCULATE PORTAL ISLANDS
        for (int i = 0; i < WindowNodes.Length; i++)
        {
            int portalStart = WindowNodes[i].PorPtr;
            int portalCount = WindowNodes[i].PorCnt;
            for(int j = portalStart; j < portalStart + portalCount; j++)
            {
                PortalNode firstPortalNode = PortalNodes[j];
                if (firstPortalNode.IsIslandValid()) { continue; }
                int suitableIndex = GetSuitableIslandIndex();
                RunDepthFirstSearch(j, suitableIndex, dfsStack);
            }
        }
        

        //CALCULATE SECTOR ISLANDS
        for(int i = 0; i< SectorNodes.Length; i++)
        {
            SectorNode sectorNode = SectorNodes[i];
            int winStart = sectorNode.SecToWinPtr;
            int winCnt = sectorNode.SecToWinCnt;

            ResetIslandCalculationMatrix(islandCalculationMatrix, i);
            bool hasUnreachable = false;
            int islandCount = 0;
            int lastIslandPortalIndex = 0;

            for(int j = winStart; j < winStart + winCnt; j++)
            {
                int windowIndex = SecToWinPtrs[j];
                WindowNode windowNode = WindowNodes[windowIndex];
                int porStart = windowNode.PorPtr;
                int porCount = windowNode.PorCnt;
                for(int k = porStart; k < porStart + porCount; k++)
                {
                    PortalNode portalNode = PortalNodes[k];
                    int local1dAtSector = FlowFieldUtilities.GetLocal1dInSector(portalNode, i, SectorMatrixColAmount, SectorColAmount);
                    if (islandCalculationMatrix[local1dAtSector] != int.MinValue) { continue; }
                    InsertForIslandCalculationMatrixDFS(local1dAtSector, k, islandCalculationMatrix, dfsStack);
                    islandCount++;
                    lastIslandPortalIndex = k;
                }
            }

            //LOOK FOR UNREACHABLE TILES AND INSERT
            for(int j = 0; j < islandCalculationMatrix.Length; j++)
            {
                if (islandCalculationMatrix[j] == int.MinValue)
                {
                    int suitableIslandIndex = GetSuitableIslandIndex();
                    InsertForIslandCalculationMatrixDFS(j, -suitableIslandIndex, islandCalculationMatrix, dfsStack);
                    islandCount++;
                    hasUnreachable = true;
                }
            }
            if(islandCount == 0)
            {
                sectorNode.SectorIslandPortalIndex = -1;
                sectorNode.IsIslandField = false;
                SectorNodes[i] = sectorNode;
            }
            else if(islandCount == 1 && !hasUnreachable)
            {
                sectorNode.SectorIslandPortalIndex = lastIslandPortalIndex;
                sectorNode.IsIslandField = false;
                SectorNodes[i] = sectorNode;
            }
            else
            {
                sectorNode.SectorIslandPortalIndex = -1;
                sectorNode.IsIslandField = true;
                SectorNodes[i] = sectorNode;

                UnsafeList<int> islandFieldForSector = IslandFields[i];
                islandFieldForSector.Length = islandCalculationMatrix.Length;
                islandFieldForSector.CopyFrom(islandCalculationMatrix);

                IslandFields[i] = islandFieldForSector;
            }
        }
    }

    void ResetIslandCalculationMatrix(UnsafeList<int> islandCalculationMatrix, int sectorIndex)
    {
        UnsafeList<byte> costs = CostsL[sectorIndex];
        for(int i = 0; i < islandCalculationMatrix.Length; i++)
        {
            islandCalculationMatrix[i] = math.select(int.MinValue, int.MaxValue, costs[i] == byte.MaxValue);
        }
    }
    bool ListContains(UnsafeList<int> list, int data)
    {
        for(int i = 0; i < list.Length; i++)
        {
            if (list[i] == data) { return true; }
        }
        return false;
    }

    void InsertForIslandCalculationMatrixDFS(int localIndex1d, int islandPortalIndex, UnsafeList<int> islandCalculationMatrix, UnsafeStack<int> stack)
    {
        //HANDLE FIRST
        islandCalculationMatrix[localIndex1d] = islandPortalIndex;
        stack.Push(localIndex1d);

        while (!stack.IsEmpty())
        {
            int curPortalIndex = stack.Pop();

            int nLocal1d = curPortalIndex + SectorColAmount;
            int eLocal1d = curPortalIndex + 1;
            int sLocal1d = curPortalIndex - SectorColAmount;
            int wLocal1d = curPortalIndex - 1;
            int neLocal1d = curPortalIndex + SectorColAmount + 1;
            int seLocal1d = curPortalIndex - SectorColAmount + 1;
            int swLocal1d = curPortalIndex - SectorColAmount - 1;
            int nwLocal1d = curPortalIndex + SectorColAmount - 1;

            bool nLocalOverflow = nLocal1d >= SectorTileAmount;
            bool eLocalOverflow = (eLocal1d % SectorColAmount) == 0;
            bool sLocalOverflow = sLocal1d < 0;
            bool wLocalOverflow = (curPortalIndex % SectorColAmount) == 0;

            nLocal1d = math.select(nLocal1d, curPortalIndex, nLocalOverflow);
            eLocal1d = math.select(eLocal1d, curPortalIndex, eLocalOverflow);
            sLocal1d = math.select(sLocal1d, curPortalIndex, sLocalOverflow);
            wLocal1d = math.select(wLocal1d, curPortalIndex, wLocalOverflow);
            neLocal1d = math.select(neLocal1d, curPortalIndex, nLocalOverflow || eLocalOverflow);
            seLocal1d = math.select(seLocal1d, curPortalIndex, sLocalOverflow || eLocalOverflow);
            swLocal1d = math.select(swLocal1d, curPortalIndex, sLocalOverflow || wLocalOverflow);
            nwLocal1d = math.select(nwLocal1d, curPortalIndex, nLocalOverflow || wLocalOverflow);

            int nIsland = islandCalculationMatrix[nLocal1d];
            int eIsland = islandCalculationMatrix[eLocal1d];
            int sIsland = islandCalculationMatrix[sLocal1d];
            int wIsland = islandCalculationMatrix[wLocal1d];
            int neIsland = islandCalculationMatrix[neLocal1d];
            int seIsland = islandCalculationMatrix[seLocal1d];
            int swIsland = islandCalculationMatrix[swLocal1d];
            int nwIsland = islandCalculationMatrix[nwLocal1d];

            bool nBlocked = nIsland == int.MaxValue;
            bool eBlocked = eIsland == int.MaxValue;
            bool sBlocked = sIsland == int.MaxValue;
            bool wBlocked = wIsland == int.MaxValue;

            bool pushn = !nLocalOverflow && nIsland == int.MinValue;
            bool pushe = !eLocalOverflow && eIsland == int.MinValue;
            bool pushs = !sLocalOverflow && sIsland == int.MinValue;
            bool pushw = !wLocalOverflow && wIsland == int.MinValue;
            bool pushne = !(nLocalOverflow || eLocalOverflow || nBlocked || eBlocked) && neIsland == int.MinValue;
            bool pushse = !(sLocalOverflow || eLocalOverflow || sBlocked || eBlocked) && seIsland == int.MinValue;
            bool pushsw = !(sLocalOverflow || wLocalOverflow || sBlocked || wBlocked) && swIsland == int.MinValue;
            bool pushnw = !(nLocalOverflow || wLocalOverflow || nBlocked || wBlocked) && nwIsland == int.MinValue;
            if (pushn) { islandCalculationMatrix[nLocal1d] = islandPortalIndex; stack.Push(nLocal1d); }
            if (pushe) { islandCalculationMatrix[eLocal1d] = islandPortalIndex; stack.Push(eLocal1d); }
            if (pushs) { islandCalculationMatrix[sLocal1d] = islandPortalIndex; stack.Push(sLocal1d); }
            if (pushw) { islandCalculationMatrix[wLocal1d] = islandPortalIndex; stack.Push(wLocal1d); }
            if (pushne) { islandCalculationMatrix[neLocal1d] = islandPortalIndex; stack.Push(neLocal1d); }
            if (pushse) { islandCalculationMatrix[seLocal1d] = islandPortalIndex; stack.Push(seLocal1d); }
            if (pushsw) { islandCalculationMatrix[swLocal1d] = islandPortalIndex; stack.Push(swLocal1d); }
            if (pushnw) { islandCalculationMatrix[nwLocal1d] = islandPortalIndex; stack.Push(nwLocal1d); }
        }
    }

    int GetSuitableIslandIndex()
    {
        for(int i = 1; i < Islands.Length; i++)
        {
            if(Islands[i] == IslandData.Removed)
            {
                Islands[i] = IslandData.Clean;
                return i;
            }
        }
        Islands.Add(IslandData.Clean);
        return Islands.Length - 1;
    }

    void RunDepthFirstSearch(int startNodeIndex, int islandIndex, UnsafeStack<int> stack)
    {

        //HANDLE FIRST
        PortalNode startNode = PortalNodes[startNodeIndex];
        startNode.IslandIndex = islandIndex;
        PortalNodes[startNodeIndex] = startNode;
        stack.Push(startNodeIndex);

        while (!stack.IsEmpty())
        {
            int curPortalIndex = stack.Pop();
            PortalNode curNode = PortalNodes[curPortalIndex];

            //PUSH NEIGHBOURS
            int p1NeighbourStart = curNode.Portal1.PorToPorPtr;
            int p1NeighbourCount = curNode.Portal1.PorToPorCnt;
            int p2NeighbourStart = curNode.Portal2.PorToPorPtr;
            int p2NeighbourCount = curNode.Portal2.PorToPorCnt;

            for(int i = p1NeighbourStart; i < p1NeighbourStart + p1NeighbourCount; i++)
            {
                PortalToPortal portalEdge = PortalEdges[i];
                PortalNode neighbourPortal = PortalNodes[portalEdge.Index];
                if (neighbourPortal.IsIslandValid()) { continue; }
                neighbourPortal.IslandIndex = islandIndex;
                PortalNodes[portalEdge.Index] = neighbourPortal;
                stack.Push(portalEdge.Index);
            }
            for (int i = p2NeighbourStart; i < p2NeighbourStart + p2NeighbourCount; i++)
            {
                PortalToPortal portalEdge = PortalEdges[i];
                PortalNode neighbourPortal = PortalNodes[portalEdge.Index];
                if (neighbourPortal.IsIslandValid()) { continue; }
                neighbourPortal.IslandIndex = islandIndex;
                PortalNodes[portalEdge.Index] = neighbourPortal;
                stack.Push(portalEdge.Index);
            }
        }
    }







    private struct UnsafeStack<T> where T : unmanaged
    {
        UnsafeList<T> _data;
        public UnsafeStack(int placeHolderDataDoingNothing)
        {
            _data = new UnsafeList<T>(0, Allocator.Temp);
        }
        public void Push(T item)
        {
            _data.Add(item);
        }
        public T Pop()
        {
            if(_data.Length == 0) { return default(T); }
            T item = _data[_data.Length - 1];
            _data.Length = _data.Length - 1;
            return item;
        }
        public bool IsEmpty()
        {
            return _data.IsEmpty;
        }
        public void Clear()
        {
            _data.Clear();
        }
    }
}
