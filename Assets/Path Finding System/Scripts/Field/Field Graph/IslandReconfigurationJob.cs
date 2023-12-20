using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;

[BurstCompile]
public struct IslandReconfigurationJob : IJob
{
    public int Offset;
    public int SectorTileAmount;
    public int SectorMatrixColAmount;
    public int SectorColAmount;
    [ReadOnly] public NativeArray<byte> CostsL;
    [ReadOnly] public NativeArray<PortalToPortal> PortalEdges;
    [ReadOnly] public NativeArray<WindowNode> WindowNodes;
    [ReadOnly] public NativeArray<int> SecToWinPtrs;
    [ReadOnly] public NativeList<int> EditedSectorIndicies;
    public UnsafeList<SectorNode> SectorNodes;
    public UnsafeList<UnsafeList<int>> IslandFields;
    public UnsafeList<PortalNode> PortalNodes;
    public NativeList<IslandData> Islands;

    public void Execute()
    {
        //PREALLOCATIONS
        UnsafeList<int> islandCalculationMatrix = new UnsafeList<int>(SectorTileAmount, Allocator.Temp);
        islandCalculationMatrix.Length = SectorTileAmount;
        UnsafeStack<int> dfsStack = new UnsafeStack<int>(0);
        NativeArray<int> editedSectorIndicies = EditedSectorIndicies;


        //HANDLE PORTAL ISLANDS
        for (int i = 0; i < editedSectorIndicies.Length; i++)
        {
            int sectorIndex = editedSectorIndicies[i];
            SectorNode sector = SectorNodes[sectorIndex];

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
                    if (Islands[portal.IslandIndex] == IslandData.Clean) { continue; }
                    int suitableIslandIndex = GetSuitableIslandIndex();
                    RunDepthFirstSearch(k, suitableIslandIndex, dfsStack);
                }
            }
        }
        
        //MARK SECTOR ISLANDS
        for (int i = 0; i < editedSectorIndicies.Length; i++)
        {
            int sectorIndex = editedSectorIndicies[i];
            SectorNode sector = SectorNodes[sectorIndex];
            if(sector.IsIslandValid() || sector.IsIslandField) { continue; }

            int winStart = sector.SecToWinPtr;
            int winCnt = sector.SecToWinCnt;

            ResetIslandCalculationMatrix(islandCalculationMatrix, sectorIndex);
            bool hasUnreachable = false;
            int islandCount = 0;
            int lastIslandPortalIndex = 0;

            for (int j = winStart; j < winStart + winCnt; j++)
            {
                int windowIndex = SecToWinPtrs[j];
                WindowNode windowNode = WindowNodes[windowIndex];
                int porStart = windowNode.PorPtr;
                int porCount = windowNode.PorCnt;
                for (int k = porStart; k < porStart + porCount; k++)
                {
                    PortalNode portalNode = PortalNodes[k];
                    
                    int local1dAtSector = FlowFieldUtilities.GetLocal1dInSector(portalNode, sectorIndex, SectorMatrixColAmount, SectorColAmount);
                    if (portalNode.WinPtr != windowIndex) { UnityEngine.Debug.Log(k - porStart + " and " + porCount); }
                    if (local1dAtSector < 0)
                    {
                        UnityEngine.Debug.Log("offset: " + Offset);
                    }
                    if (islandCalculationMatrix[local1dAtSector] != int.MinValue) { continue; }
                    InsertForIslandCalculationMatrixDFS(local1dAtSector, k, islandCalculationMatrix, dfsStack);
                    islandCount++;
                    lastIslandPortalIndex = k;
                }
            }
            
            //LOOK FOR UNREACHABLE TILES AND INSERT
            for (int j = 0; j < islandCalculationMatrix.Length; j++)
            {
                if (islandCalculationMatrix[j] == int.MinValue)
                {
                    int suitableIslandIndex = GetSuitableIslandIndex();
                    InsertForIslandCalculationMatrixDFS(j, -suitableIslandIndex, islandCalculationMatrix, dfsStack);
                    islandCount++;
                    hasUnreachable = true;
                }
            }

            if (islandCount == 0)
            {
                sector.SectorIslandPortalIndex = -1;
                sector.IsIslandField = false;
                SectorNodes[sectorIndex] = sector;
            }
            else if (islandCount == 1 && !hasUnreachable)
            {
                sector.SectorIslandPortalIndex = lastIslandPortalIndex;
                sector.IsIslandField = false;
                SectorNodes[sectorIndex] = sector;
            }
            else
            {
                sector.SectorIslandPortalIndex = -1;
                sector.IsIslandField = true;
                SectorNodes[sectorIndex] = sector;

                UnsafeList<int> islandFieldForSector = IslandFields[sectorIndex];
                islandFieldForSector.Length = islandCalculationMatrix.Length;
                islandFieldForSector.CopyFrom(islandCalculationMatrix);

                IslandFields[sectorIndex] = islandFieldForSector;
            }
        }



        //REMOVE DIRTY ISLANDS
        for(int i = 1; i < Islands.Length; i++)
        {
            IslandData island = Islands[i];
            Islands[i] = island == IslandData.Dirty ? IslandData.Removed : island;
        }

        //DISPOSE UNUSED ISLAND FIELDS
        for(int i = 0; i < IslandFields.Length; i++)
        {
            UnsafeList<int> islandField = IslandFields[i];
            if(islandField.Length != 0) { continue; }
            islandField.TrimExcess();
            IslandFields[i] = islandField;
        }
    }
    void ResetIslandCalculationMatrix(UnsafeList<int> islandCalculationMatrix, int sectorIndex)
    {
        NativeSlice<byte> costs = new NativeSlice<byte>(CostsL, sectorIndex * SectorTileAmount, SectorTileAmount);
        for (int i = 0; i < islandCalculationMatrix.Length; i++)
        {
            islandCalculationMatrix[i] = math.select(int.MinValue, int.MaxValue, costs[i] == byte.MaxValue);
        }
    }
    bool ListContains(UnsafeList<int> list, int data)
    {
        for (int i = 0; i < list.Length; i++)
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
        for (int i = 1; i < Islands.Length; i++)
        {
            if (Islands[i] == IslandData.Removed)
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

            for (int i = p1NeighbourStart; i < p1NeighbourStart + p1NeighbourCount; i++)
            {
                PortalToPortal portalEdge = PortalEdges[i];
                PortalNode neighbourPortal = PortalNodes[portalEdge.Index];
                if (neighbourPortal.IsIslandValid() && Islands[neighbourPortal.IslandIndex] == IslandData.Clean) { continue; }
                neighbourPortal.IslandIndex = islandIndex;
                PortalNodes[portalEdge.Index] = neighbourPortal;
                stack.Push(portalEdge.Index);
            }
            for (int i = p2NeighbourStart; i < p2NeighbourStart + p2NeighbourCount; i++)
            {
                PortalToPortal portalEdge = PortalEdges[i];
                PortalNode neighbourPortal = PortalNodes[portalEdge.Index];
                if (neighbourPortal.IsIslandValid() && Islands[neighbourPortal.IslandIndex] == IslandData.Clean) { continue; }
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
            if (_data.Length == 0) { return default(T); }
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