using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;

namespace FlowFieldNavigation
{
    [BurstCompile]
    internal struct PortalReductionJob : IJob
    {
        internal int2 TargetIndex;
        internal float TileSize;
        internal int FieldColAmount;
        internal int FieldRowAmount;
        internal float FieldTileSize;
        internal int SectorColAmount;
        internal int SectorMatrixColAmount;
        internal int SectorTileAmount;
        internal float2 FieldGridStartPos;

        internal NativeArray<PortalTraversalData> PortalTraversalDataArray;

        internal UnsafeList<PathSectorState> SectorStateTable;
        internal NativeList<int> PickedToSector;
        internal UnsafeList<DijkstraTile> TargetSectorCosts;
        internal NativeReference<int> FlowFieldLength;

        [ReadOnly] internal NativeSlice<float2> SourcePositions;
        [ReadOnly] internal NativeArray<SectorNode> SectorNodes;
        [ReadOnly] internal NativeArray<int> SecToWinPtrs;
        [ReadOnly] internal NativeArray<WindowNode> WindowNodes;
        [ReadOnly] internal NativeArray<int> WinToSecPtrs;
        [ReadOnly] internal NativeArray<PortalNode> PortalNodes;
        [ReadOnly] internal NativeArray<PortalToPortal> PorPtrs;
        [ReadOnly] internal NativeArray<byte> Costs;
        [ReadOnly] internal NativeArray<SectorDirectionData> LocalDirections;
        [ReadOnly] internal NativeArray<UnsafeList<int>> IslandFields;

        internal NativeList<int> TargetNeighbourPortalIndicies;
        internal NativeList<int> SourcePortalIndexList;
        internal NativeList<int> DijkstraStartIndicies;

        int _targetSectorIndex1d;
        public void Execute()
        {
            DijkstraStartIndicies.Clear();
            SourcePortalIndexList.Clear();
            //TARGET DATA
            int2 targetSectorIndex2d = new int2(TargetIndex.x / SectorColAmount, TargetIndex.y / SectorColAmount);
            _targetSectorIndex1d = targetSectorIndex2d.y * SectorMatrixColAmount + targetSectorIndex2d.x;
            int targetGeneralIndex1d = TargetIndex.y * FieldColAmount + TargetIndex.x;
            if (PickedToSector.Length == 0)
            {
                SetIntegratedCosts(targetGeneralIndex1d);
                PortalTraversalData destinationData = PortalTraversalDataArray[PortalTraversalDataArray.Length - 1];
                destinationData.DistanceFromTarget = 0;
                PortalTraversalDataArray[PortalTraversalDataArray.Length - 1] = destinationData;
                SetTargetNeighbourPortalDataAndAddToList();
            }

            SingleFloatUnsafeHeap<int> walkerHeap = new SingleFloatUnsafeHeap<int>(10, Allocator.Temp);
            SetSourcePortalIndicies();
            NativeArray<int> sourcePortalsAsArray = SourcePortalIndexList.AsArray();
            NativeList<int> aStarTraversedIndicies = new NativeList<int>(Allocator.Temp);
            for (int i = 0; i < sourcePortalsAsArray.Length; i++)
            {
                int stoppedIndex = RunReductionAStar(sourcePortalsAsArray[i], walkerHeap, aStarTraversedIndicies);
                if (stoppedIndex == -1) { continue; }
                NewPickAStarNodes(stoppedIndex);
                ResetTraversedIndicies(aStarTraversedIndicies);
                walkerHeap.Clear();
            }
        }
        void ResetTraversedIndicies(NativeList<int> aStarTraversedIndicies)
        {
            PortalTraversalMark bitsToSet = ~(PortalTraversalMark.AStarTraversed | PortalTraversalMark.AStarExtracted);
            for (int i = 0; i < aStarTraversedIndicies.Length; i++)
            {
                int index = aStarTraversedIndicies[i];
                PortalTraversalData travData = PortalTraversalDataArray[index];
                travData.Mark &= bitsToSet;
                PortalTraversalDataArray[index] = travData;
            }
            aStarTraversedIndicies.Clear();
        }
        void NewPickAStarNodes(int stoppedIndex)
        {
            int curNodeIndex = stoppedIndex;
            while (curNodeIndex != -1)
            {
                PortalTraversalData curPortalData = PortalTraversalDataArray[curNodeIndex];
                curPortalData.Mark |= PortalTraversalMark.AStarPicked;
                PortalTraversalDataArray[curNodeIndex] = curPortalData;
                curNodeIndex = curPortalData.OriginIndex;
            }
        }
        int RunReductionAStar(int sourcePortalIndex, SingleFloatUnsafeHeap<int> traversalHeap, NativeList<int> aStarTraversedIndicies)
        {
            //Handle initial portal
            PortalTraversalData sourceData = PortalTraversalDataArray[sourcePortalIndex];
            if (sourceData.HasMark(PortalTraversalMark.AStarPicked) || sourceData.HasMark(PortalTraversalMark.DijkstraTraversed))
            {
                return -1;
            }
            sourceData.Mark |= PortalTraversalMark.AStarPicked | PortalTraversalMark.AStarTraversed | PortalTraversalMark.AStarExtracted | PortalTraversalMark.Reduced;
            sourceData.OriginIndex = -1;
            PortalTraversalDataArray[sourcePortalIndex] = sourceData;
            aStarTraversedIndicies.Add(sourcePortalIndex);
            PortalNode sourceNode = PortalNodes[sourcePortalIndex];
            int sourcePor1P2pIdx = sourceNode.Portal1.PorToPorPtr;
            int sourcePor2P2pIdx = sourceNode.Portal2.PorToPorPtr;
            int sourcePor1P2pCnt = sourceNode.Portal1.PorToPorCnt;
            int sourcePor2P2pCnt = sourceNode.Portal2.PorToPorCnt;
            TraverseNeighbours(sourceData, aStarTraversedIndicies, ref traversalHeap, sourcePortalIndex, sourcePor1P2pIdx, sourcePor1P2pCnt);
            TraverseNeighbours(sourceData, aStarTraversedIndicies, ref traversalHeap, sourcePortalIndex, sourcePor2P2pIdx, sourcePor2P2pCnt);

            //Handle remaining portals
            int curPortalIndex;
            PortalTraversalData curData;
            SetNextNode(ref traversalHeap, out curPortalIndex, out curData);
            while (!(curData.IsGoal() || ShouldMerge(curData.Mark)))
            {
                PortalNode curNode = PortalNodes[curPortalIndex];
                int por1P2pIdx = curNode.Portal1.PorToPorPtr;
                int por2P2pIdx = curNode.Portal2.PorToPorPtr;
                int por1P2pCnt = curNode.Portal1.PorToPorCnt;
                int por2P2pCnt = curNode.Portal2.PorToPorCnt;
                TraverseNeighbours(curData, aStarTraversedIndicies, ref traversalHeap, curPortalIndex, por1P2pIdx, por1P2pCnt);
                TraverseNeighbours(curData, aStarTraversedIndicies, ref traversalHeap, curPortalIndex, por2P2pIdx, por2P2pCnt);
                SetNextNode(ref traversalHeap, out curPortalIndex, out curData);
            }
            return curPortalIndex;
        }
        void SetNextNode(ref SingleFloatUnsafeHeap<int> traversalHeap, out int curPortalIndex, out PortalTraversalData curData)
        {
            int nextMinIndex = traversalHeap.ExtractMin();
            PortalTraversalData nextMinTraversalData = PortalTraversalDataArray[nextMinIndex];
            while (nextMinTraversalData.HasMark(PortalTraversalMark.AStarExtracted))
            {
                nextMinIndex = traversalHeap.ExtractMin();
                nextMinTraversalData = PortalTraversalDataArray[nextMinIndex];
            }
            nextMinTraversalData.Mark |= PortalTraversalMark.AStarExtracted;
            PortalTraversalDataArray[nextMinIndex] = nextMinTraversalData;
            curPortalIndex = nextMinIndex;
            curData = nextMinTraversalData;
        }
        void TraverseNeighbours(PortalTraversalData curData, NativeList<int> aStarTraversedIndicies, ref SingleFloatUnsafeHeap<int> traversalHeap, int curNodeIndex, int neighbourPointerStart, int neighbourPointerCount)
        {
            bool curDijkstraTraversed = curData.HasMark(PortalTraversalMark.DijkstraTraversed);
            for (int i = neighbourPointerStart; i < neighbourPointerStart + neighbourPointerCount; i++)
            {
                PortalToPortal neighbourConnection = PorPtrs[i];
                PortalNode portalNode = PortalNodes[neighbourConnection.Index];
                PortalTraversalData traversalData = PortalTraversalDataArray[neighbourConnection.Index];
                if (traversalData.HasMark(PortalTraversalMark.AStarTraversed))
                {
                    float newGCost = curData.GCost + neighbourConnection.Distance;
                    if (newGCost < traversalData.GCost)
                    {
                        float newFCost = traversalData.HCost + newGCost;
                        traversalData.GCost = newGCost;
                        traversalData.FCost = newFCost;
                        traversalData.OriginIndex = curNodeIndex;
                        PortalTraversalDataArray[neighbourConnection.Index] = traversalData;
                        traversalHeap.Add(neighbourConnection.Index, traversalData.FCost);
                    }
                }
                else
                {
                    float hCost = GetHCost(portalNode.Portal1.Index);
                    float gCost = curData.GCost + neighbourConnection.Distance;
                    float fCost = hCost + gCost;
                    traversalData.HCost = hCost;
                    traversalData.GCost = gCost;
                    traversalData.FCost = fCost;
                    traversalData.Mark |= PortalTraversalMark.AStarTraversed | PortalTraversalMark.Reduced;
                    traversalData.OriginIndex = curNodeIndex;
                    PortalTraversalDataArray[neighbourConnection.Index] = traversalData;
                    traversalHeap.Add(neighbourConnection.Index, traversalData.FCost);
                    aStarTraversedIndicies.Add(neighbourConnection.Index);

                    bool neighbourDijkstraTraversed = traversalData.HasMark(PortalTraversalMark.DijkstraTraversed);
                    if (!curDijkstraTraversed && neighbourDijkstraTraversed)
                    {
                        DijkstraStartIndicies.Add(neighbourConnection.Index);
                    }
                }
            }
            if (curData.HasMark(PortalTraversalMark.TargetNeighbour))
            {
                int targetNodeIndex = PortalNodes.Length - 1;
                PortalTraversalData traversalData = PortalTraversalDataArray[targetNodeIndex];
                if (traversalData.HasMark(PortalTraversalMark.AStarTraversed))
                {
                    float newGCost = curData.GCost + curData.DistanceFromTarget;
                    if (newGCost < traversalData.GCost)
                    {
                        float newFCost = traversalData.HCost + newGCost;
                        traversalData.GCost = newGCost;
                        traversalData.FCost = newFCost;
                        traversalData.OriginIndex = curNodeIndex;
                        PortalTraversalDataArray[targetNodeIndex] = traversalData;
                        traversalHeap.Add(targetNodeIndex, traversalData.FCost);
                    }
                }
                else
                {
                    float hCost = 0f;
                    float gCost = curData.GCost + curData.DistanceFromTarget;
                    float fCost = hCost + gCost;
                    traversalData.HCost = hCost;
                    traversalData.GCost = gCost;
                    traversalData.FCost = fCost;
                    traversalData.Mark |= PortalTraversalMark.AStarTraversed;
                    traversalData.OriginIndex = curNodeIndex;
                    PortalTraversalDataArray[targetNodeIndex] = traversalData;
                    traversalHeap.Add(targetNodeIndex, traversalData.FCost);
                    aStarTraversedIndicies.Add(targetNodeIndex);
                }
            }
        }
        bool ShouldMerge(PortalTraversalMark traversalMarks)
        {
            return (traversalMarks & PortalTraversalMark.AStarPicked) == PortalTraversalMark.AStarPicked;
        }
        float GetHCost(Index2 nodePos)
        {
            int2 newNodePos = nodePos;
            int2 targetPos = TargetIndex;
            return math.distance(newNodePos * new float2(TileSize, TileSize), targetPos * new float2(TileSize, TileSize));
        }
        internal int GetIsland(int2 general2d)
        {
            int2 sector2d = FlowFieldUtilities.GetSector2D(general2d, SectorColAmount);
            int sector1d = FlowFieldUtilities.To1D(sector2d, SectorMatrixColAmount);
            SectorNode sector = SectorNodes[sector1d];

            if (sector.IsIslandValid())
            {
                return PortalNodes[sector.SectorIslandPortalIndex].IslandIndex;
            }
            else if (sector.IsIslandField)
            {
                int2 sectorStart = FlowFieldUtilities.GetSectorStartIndex(sector2d, SectorColAmount);
                int2 local2d = FlowFieldUtilities.GetLocal2D(general2d, sectorStart);
                int local1d = FlowFieldUtilities.To1D(local2d, SectorColAmount);
                int island = IslandFields[sector1d][local1d];
                switch (island)
                {
                    case < 0:
                        return -island;
                    case int.MaxValue:
                        return int.MaxValue;
                    default:
                        return PortalNodes[island].IslandIndex;
                }
            }
            return int.MaxValue;
        }
        void SetSourcePortalIndicies()
        {
            int targetIsland = GetIsland(TargetIndex);
            int sectorTileAmount = SectorColAmount * SectorColAmount;
            for (int i = 0; i < SourcePositions.Length; i++)
            {
                float2 sourcePos = SourcePositions[i];
                int2 sourceIndex = FlowFieldUtilities.PosTo2D(sourcePos, FieldTileSize, FieldGridStartPos);
                int2 sourceSectorIndex = sourceIndex / SectorColAmount;
                int sourceSectorIndexFlat = sourceSectorIndex.y * SectorMatrixColAmount + sourceSectorIndex.x;
                //ADD SOURCE SECTOR TO THE PICKED SECTORS
                PathSectorState sectorState = SectorStateTable[sourceSectorIndexFlat];
                if ((sectorState & PathSectorState.Included) != PathSectorState.Included)
                {
                    PickedToSector.Add(sourceSectorIndexFlat);
                    SectorStateTable[sourceSectorIndexFlat] |= PathSectorState.Included | PathSectorState.Source;
                    SetSectorPortalIndicies(sourceSectorIndexFlat, SourcePortalIndexList, targetIsland);
                }
                else if ((sectorState & PathSectorState.Source) != PathSectorState.Source)
                {
                    SectorStateTable[sourceSectorIndexFlat] |= PathSectorState.Source;
                    SetSectorPortalIndicies(sourceSectorIndexFlat, SourcePortalIndexList, targetIsland);
                }
            }
        }
        void SetSectorPortalIndicies(int targetSectorIndexF, NativeList<int> destinationList, int targetIsland)
        {
            SectorNode sectorNode = SectorNodes[targetSectorIndexF];
            int winPtr = sectorNode.SecToWinPtr;
            int winCnt = sectorNode.SecToWinCnt;
            for (int i = 0; i < winCnt; i++)
            {
                WindowNode windowNode = WindowNodes[SecToWinPtrs[winPtr + i]];
                int porPtr = windowNode.PorPtr;
                int porCnt = windowNode.PorCnt;
                for (int j = 0; j < porCnt; j++)
                {
                    if (PortalNodes[j + porPtr].IslandIndex != targetIsland) { continue; }
                    destinationList.Add(j + porPtr);
                }
            }
        }
        void SetTargetNeighbourPortalDataAndAddToList()
        {
            SectorNode sectorNode = SectorNodes[_targetSectorIndex1d];
            int winPtr = sectorNode.SecToWinPtr;
            int winCnt = sectorNode.SecToWinCnt;
            for (int i = 0; i < winCnt; i++)
            {
                WindowNode windowNode = WindowNodes[SecToWinPtrs[winPtr + i]];
                int porPtr = windowNode.PorPtr;
                int porCnt = windowNode.PorCnt;
                for (int j = porPtr; j < porCnt + porPtr; j++)
                {
                    int portalNodeIndex = j;
                    int portalLocalIndexAtSector = FlowFieldUtilities.GetLocal1dInSector(PortalNodes[portalNodeIndex], _targetSectorIndex1d, SectorMatrixColAmount, SectorColAmount);
                    float integratedCost = TargetSectorCosts[portalLocalIndexAtSector].IntegratedCost;
                    if (integratedCost == float.MaxValue) { continue; }
                    PortalTraversalData portalData = PortalTraversalDataArray[portalNodeIndex];
                    portalData.DistanceFromTarget = integratedCost;
                    portalData.Mark = PortalTraversalMark.TargetNeighbour;
                    PortalTraversalDataArray[portalNodeIndex] = portalData;
                    TargetNeighbourPortalIndicies.Add(portalNodeIndex);
                }
            }
        }
        void SetIntegratedCosts(int targetIndex)
        {
            NativeQueue<int> aStarQueue = new NativeQueue<int>(Allocator.Temp);
            CalculateIntegratedCosts(TargetSectorCosts, aStarQueue, SectorNodes[_targetSectorIndex1d].Sector, targetIndex);
        }
        void CalculateIntegratedCosts(UnsafeList<DijkstraTile> integratedCosts, NativeQueue<int> aStarQueue, Sector sector, int targetIndexFlat)
        {
            //DATA
            int2 targetIndex2d = FlowFieldUtilities.To2D(targetIndexFlat, FieldColAmount);
            NativeSlice<byte> costs = new NativeSlice<byte>(Costs, _targetSectorIndex1d * SectorTileAmount, SectorTileAmount);

            //CODE

            Reset();
            int targetLocalIndex = FlowFieldUtilities.GetLocal1D(targetIndex2d, SectorColAmount, SectorMatrixColAmount).index;
            DijkstraTile targetTile = integratedCosts[targetLocalIndex];
            targetTile.IntegratedCost = 0f;
            targetTile.IsAvailable = false;
            integratedCosts[targetLocalIndex] = targetTile;
            Enqueue(LocalDirections[targetLocalIndex]);
            while (!aStarQueue.IsEmpty())
            {
                int localindex = aStarQueue.Dequeue();
                DijkstraTile tile = integratedCosts[localindex];
                tile.IntegratedCost = GetCost(LocalDirections[localindex]);
                integratedCosts[localindex] = tile;
                Enqueue(LocalDirections[localindex]);
            }

            //HELPERS

            void Reset()
            {
                for (int i = 0; i < integratedCosts.Length; i++)
                {
                    byte cost = costs[i];
                    if (cost == byte.MaxValue)
                    {
                        integratedCosts[i] = new DijkstraTile(cost, float.MaxValue, false);
                        continue;
                    }
                    integratedCosts[i] = new DijkstraTile(cost, float.MaxValue, true);
                }
            }
            void Enqueue(SectorDirectionData localDirections)
            {
                int n = localDirections.N;
                int e = localDirections.E;
                int s = localDirections.S;
                int w = localDirections.W;
                if (integratedCosts[n].IsAvailable)
                {
                    aStarQueue.Enqueue(n);
                    DijkstraTile tile = integratedCosts[n];
                    tile.IsAvailable = false;
                    integratedCosts[n] = tile;
                }
                if (integratedCosts[e].IsAvailable)
                {
                    aStarQueue.Enqueue(e);
                    DijkstraTile tile = integratedCosts[e];
                    tile.IsAvailable = false;
                    integratedCosts[e] = tile;
                }
                if (integratedCosts[s].IsAvailable)
                {
                    aStarQueue.Enqueue(s);
                    DijkstraTile tile = integratedCosts[s];
                    tile.IsAvailable = false;
                    integratedCosts[s] = tile;
                }
                if (integratedCosts[w].IsAvailable)
                {
                    aStarQueue.Enqueue(w);
                    DijkstraTile tile = integratedCosts[w];
                    tile.IsAvailable = false;
                    integratedCosts[w] = tile;
                }
            }
            float GetCost(SectorDirectionData localDirections)
            {
                float costToReturn = float.MaxValue;
                float nCost = integratedCosts[localDirections.N].IntegratedCost + 1f;
                float neCost = integratedCosts[localDirections.NE].IntegratedCost + 1.4f;
                float eCost = integratedCosts[localDirections.E].IntegratedCost + 1f;
                float seCost = integratedCosts[localDirections.SE].IntegratedCost + 1.4f;
                float sCost = integratedCosts[localDirections.S].IntegratedCost + 1f;
                float swCost = integratedCosts[localDirections.SW].IntegratedCost + 1.4f;
                float wCost = integratedCosts[localDirections.W].IntegratedCost + 1f;
                float nwCost = integratedCosts[localDirections.NW].IntegratedCost + 1.4f;
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
}
