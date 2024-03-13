﻿using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.XR;

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
        internal NativeList<PortalTraversalData> DestinationTraversalDataList;

        internal UnsafeList<PathSectorState> SectorStateTable;
        internal NativeList<int> PickedToSector;
        internal UnsafeList<float> TargetSectorCosts;

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

        internal NativeList<int> SourcePortalIndexList;
        internal NativeList<int> DijkstraStartIndicies;

        int _targetSectorIndex1d;
        public void Execute()
        {
            UnityEngine.Debug.Log(DestinationTraversalDataList.Length);
            DijkstraStartIndicies.Clear();
            SourcePortalIndexList.Clear();
            //TARGET DATA
            int2 targetSectorIndex2d = new int2(TargetIndex.x / SectorColAmount, TargetIndex.y / SectorColAmount);
            _targetSectorIndex1d = targetSectorIndex2d.y * SectorMatrixColAmount + targetSectorIndex2d.x;


            SingleFloatUnsafeHeap<PortalTraversalIndex> walkerHeap = new SingleFloatUnsafeHeap<PortalTraversalIndex>(10, Allocator.Temp);
            SetSourcePortalIndicies();
            NativeArray<int> sourcePortalsAsArray = SourcePortalIndexList.AsArray();
            NativeList<int> aStarTraversedIndicies = new NativeList<int>(Allocator.Temp);
            NativeList<int> targetNeighbourPortalIndicies = new NativeList<int>(Allocator.Temp);
            for (int i = 0; i < sourcePortalsAsArray.Length; i++)
            {
                PortalTraversalIndex stoppedIndex = RunReductionAStar(sourcePortalsAsArray[i], walkerHeap, aStarTraversedIndicies, targetNeighbourPortalIndicies);
                if (!stoppedIndex.IsValid()) { continue; }
                NewPickAStarNodes(stoppedIndex);
                ResetTraversedIndicies(aStarTraversedIndicies);
                walkerHeap.Clear();
            }

            for(int i = 0; i < targetNeighbourPortalIndicies.Length; i++)
            {
                int targetNeighbourPortalIndex = targetNeighbourPortalIndicies[i];
                DijkstraStartIndicies.Add(targetNeighbourPortalIndex);
                PortalTraversalData portalData = PortalTraversalDataArray[targetNeighbourPortalIndex];
                portalData.DistanceFromTarget++;
                PortalTraversalDataArray[targetNeighbourPortalIndex] = portalData;
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
        void NewPickAStarNodes(PortalTraversalIndex index)
        {
            int curNodeIndex = index.GetIndex(out bool isDestination);
            if (isDestination)
            {
                curNodeIndex = DestinationTraversalDataList[curNodeIndex].OriginIndex;
            }
            while (curNodeIndex != -1)
            {
                PortalTraversalData curPortalData = PortalTraversalDataArray[curNodeIndex];
                curPortalData.Mark |= PortalTraversalMark.AStarPicked;
                PortalTraversalDataArray[curNodeIndex] = curPortalData;
                curNodeIndex = curPortalData.OriginIndex;
            }
        }
        PortalTraversalIndex RunReductionAStar(int sourcePortalIndex, SingleFloatUnsafeHeap<PortalTraversalIndex> traversalHeap, NativeList<int> aStarTraversedIndicies, NativeList<int> targetNeighbourPortalIndicies)
        {
            //Handle initial portal
            PortalTraversalData sourceData = PortalTraversalDataArray[sourcePortalIndex];
            if (sourceData.HasMark(PortalTraversalMark.AStarPicked) || sourceData.HasMark(PortalTraversalMark.DijkstraTraversed))
            {
                return PortalTraversalIndex.Invalid;
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
            SubmitIfGoalSector(ref sourceData, sourcePortalIndex, sourceNode.Portal1.Index, sourceNode.Portal2.Index, targetNeighbourPortalIndicies);
            TraverseNeighbours(sourceData, aStarTraversedIndicies, ref traversalHeap, sourcePortalIndex, sourcePor1P2pIdx, sourcePor1P2pCnt);
            TraverseNeighbours(sourceData, aStarTraversedIndicies, ref traversalHeap, sourcePortalIndex, sourcePor2P2pIdx, sourcePor2P2pCnt);

            //Handle remaining portals
            PortalTraversalIndex curPortalIndex;
            PortalTraversalData curData;
            SetNextNode(ref traversalHeap, out curPortalIndex, out curData);
            while (!(curPortalIndex.IsGoal() || ShouldMerge(curData.Mark)))
            {
                int index = curPortalIndex.GetIndexUnchecked();
                PortalNode curNode = PortalNodes[index];
                int por1P2pIdx = curNode.Portal1.PorToPorPtr;
                int por2P2pIdx = curNode.Portal2.PorToPorPtr;
                int por1P2pCnt = curNode.Portal1.PorToPorCnt;
                int por2P2pCnt = curNode.Portal2.PorToPorCnt;
                SubmitIfGoalSector(ref curData, index, curNode.Portal1.Index, curNode.Portal2.Index, targetNeighbourPortalIndicies);
                TraverseNeighbours(curData, aStarTraversedIndicies, ref traversalHeap, index, por1P2pIdx, por1P2pCnt);
                TraverseNeighbours(curData, aStarTraversedIndicies, ref traversalHeap, index, por2P2pIdx, por2P2pCnt);
                SetNextNode(ref traversalHeap, out curPortalIndex, out curData);
            }
            return curPortalIndex;
        }
        void SetNextNode(ref SingleFloatUnsafeHeap<PortalTraversalIndex> traversalHeap, out PortalTraversalIndex curPortalIndex, out PortalTraversalData curData)
        {
            PortalTraversalIndex nextMinIndex = traversalHeap.ExtractMin();
            PortalTraversalData nextMinTraversalData;
            if (nextMinIndex.IsGoal())
            {
                nextMinTraversalData = DestinationTraversalDataList[nextMinIndex.GetIndexUnchecked()];
            }
            else
            {
                nextMinTraversalData = PortalTraversalDataArray[nextMinIndex.GetIndexUnchecked()];
            }

            while (nextMinTraversalData.HasMark(PortalTraversalMark.AStarExtracted))
            {
                nextMinIndex = traversalHeap.ExtractMin();
                if (nextMinIndex.IsGoal())
                {
                    nextMinTraversalData = DestinationTraversalDataList[nextMinIndex.GetIndexUnchecked()];
                }
                else
                {
                    nextMinTraversalData = PortalTraversalDataArray[nextMinIndex.GetIndexUnchecked()];
                }
            }
            nextMinTraversalData.Mark |= PortalTraversalMark.AStarExtracted;
            if (nextMinIndex.IsGoal())
            {
                DestinationTraversalDataList[nextMinIndex.GetIndexUnchecked()] = nextMinTraversalData;
            }
            else
            {
                PortalTraversalDataArray[nextMinIndex.GetIndexUnchecked()] = nextMinTraversalData;
            }
            curPortalIndex = nextMinIndex;
            curData = nextMinTraversalData;
        }
        void TraverseNeighbours(PortalTraversalData curData, NativeList<int> aStarTraversedIndicies, ref SingleFloatUnsafeHeap<PortalTraversalIndex> traversalHeap, int curNodeIndex, int neighbourPointerStart, int neighbourPointerCount)
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
                        traversalHeap.Add(new PortalTraversalIndex(neighbourConnection.Index, false), traversalData.FCost);
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
                    traversalHeap.Add(new PortalTraversalIndex(neighbourConnection.Index, false), traversalData.FCost);
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
                int targetNodeIndex = 0;
                PortalTraversalData traversalData = DestinationTraversalDataList[targetNodeIndex];
                if (traversalData.HasMark(PortalTraversalMark.AStarTraversed))
                {
                    float newGCost = curData.GCost + curData.DistanceFromTarget;
                    if (newGCost < traversalData.GCost)
                    {
                        float newFCost = traversalData.HCost + newGCost;
                        traversalData.GCost = newGCost;
                        traversalData.FCost = newFCost;
                        traversalData.OriginIndex = curNodeIndex;
                        DestinationTraversalDataList[targetNodeIndex] = traversalData;
                        traversalHeap.Add(new PortalTraversalIndex(targetNodeIndex, true), traversalData.FCost);
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
                    traversalData.Mark |= PortalTraversalMark.AStarTraversed | PortalTraversalMark.Reduced;
                    traversalData.OriginIndex = curNodeIndex;
                    DestinationTraversalDataList[targetNodeIndex] = traversalData;
                    traversalHeap.Add(new PortalTraversalIndex(targetNodeIndex, true), traversalData.FCost);
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
        void SubmitIfGoalSector(ref PortalTraversalData curTravData, int curPortalIndex, int2 portalFieldIndex1, int2 portalFieldIndex2, NativeList<int> targetNeighbourPortalIndicies)
        {
            int sector1 = FlowFieldUtilities.GetSector1D(portalFieldIndex1, SectorColAmount, SectorMatrixColAmount);
            int sector2 = FlowFieldUtilities.GetSector1D(portalFieldIndex2, SectorColAmount, SectorMatrixColAmount);
            if((sector1 == _targetSectorIndex1d || sector2 == _targetSectorIndex1d) && DestinationTraversalDataList.Length == 0)
            { 
                SetTargetSectorCosts();
                PortalTraversalData destinationData = new PortalTraversalData();
                destinationData.Reset();
                destinationData.DistanceFromTarget = 0;
                DestinationTraversalDataList.Add(destinationData);
                SetTargetNeighbourPortalDataAndAddToList(targetNeighbourPortalIndicies);
                curTravData = PortalTraversalDataArray[curPortalIndex];
            }
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
        void SetTargetNeighbourPortalDataAndAddToList(NativeList<int> targetNeighbourPortalIndicies)
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
                    float integratedCost = TargetSectorCosts[portalLocalIndexAtSector];
                    if (integratedCost == float.MaxValue) { continue; }
                    PortalTraversalData portalData = PortalTraversalDataArray[portalNodeIndex];
                    portalData.DistanceFromTarget = integratedCost;
                    portalData.Mark |= PortalTraversalMark.TargetNeighbour;
                    PortalTraversalDataArray[portalNodeIndex] = portalData;
                    targetNeighbourPortalIndicies.Add(portalNodeIndex);
                }
            }
        }
        void SetTargetSectorCosts()
        {
            int sectorColAmount = SectorColAmount;
            int sectorTileAmount = SectorTileAmount;
            UnsafeList<float> targetSectorCostsNew = TargetSectorCosts;
            NativeSlice<byte> costs = new NativeSlice<byte>(Costs, _targetSectorIndex1d * SectorTileAmount, SectorTileAmount);
            NativeBitArray isBlocked = new NativeBitArray(SectorTileAmount, Allocator.Temp);
            for(int i = 0; i < TargetSectorCosts.Length; i++)
            {
                TargetSectorCosts[i] = float.MaxValue;
                isBlocked.Set(i, costs[i] == byte.MaxValue);
            }
            NativeQueue<int> fastMarchingQueue = new NativeQueue<int>(Allocator.Temp);
            int4 directions_N_E_S_W;
            int4 directions_NE_SE_SW_NW;
            bool4 isBlocked_N_E_S_W;
            int targetLocal1d = FlowFieldUtilities.GetLocal1D(TargetIndex, SectorColAmount);
            TargetSectorCosts[targetLocal1d] = 0;
            isBlocked.Set(targetLocal1d, true);
            SetNeighbourData(targetLocal1d);
            EnqueueNeighbours();
            while (!fastMarchingQueue.IsEmpty())
            {
                int curIndex = fastMarchingQueue.Dequeue();
                SetNeighbourData(curIndex);
                TargetSectorCosts[curIndex] = GetCost();
                EnqueueNeighbours();
            }

            void SetNeighbourData(int curIndex)
            {
                directions_N_E_S_W = new int4()
                {
                    x = sectorColAmount,
                    y = 1,
                    z = -sectorColAmount,
                    w = -1
                };
                directions_N_E_S_W += curIndex;
                directions_NE_SE_SW_NW = new int4()
                {
                    x = sectorColAmount,
                    y = -sectorColAmount,
                    z = -sectorColAmount,
                    w = sectorColAmount,
                };
                directions_NE_SE_SW_NW += new int4(1, 1, -1, -1);
                directions_NE_SE_SW_NW += curIndex;
                bool4 overflow_N_E_S_W = new bool4()
                {
                    x = directions_N_E_S_W.x >= sectorTileAmount,
                    y = (directions_N_E_S_W.y % sectorColAmount) == 0,
                    z = directions_N_E_S_W.z < 0,
                    w = (curIndex % sectorColAmount) == 0,
                };
                bool4 overflow_NE_SE_SW_NW = new bool4()
                {
                    x = overflow_N_E_S_W.x || overflow_N_E_S_W.y,
                    y = overflow_N_E_S_W.z || overflow_N_E_S_W.y,
                    z = overflow_N_E_S_W.z || overflow_N_E_S_W.w,
                    w = overflow_N_E_S_W.x || overflow_N_E_S_W.w,
                };
                directions_N_E_S_W = math.select(directions_N_E_S_W, curIndex, overflow_N_E_S_W);
                directions_NE_SE_SW_NW = math.select(directions_NE_SE_SW_NW, curIndex, overflow_NE_SE_SW_NW);
                isBlocked_N_E_S_W = new bool4()
                {
                    x = isBlocked.IsSet(directions_N_E_S_W.x),
                    y = isBlocked.IsSet(directions_N_E_S_W.y),
                    z = isBlocked.IsSet(directions_N_E_S_W.z),
                    w = isBlocked.IsSet(directions_N_E_S_W.w),
                };
            }
            float GetCost()
            {
                float4 costs_N_E_S_W = new float4()
                {
                    x = targetSectorCostsNew[directions_N_E_S_W.x],
                    y = targetSectorCostsNew[directions_N_E_S_W.y],
                    z = targetSectorCostsNew[directions_N_E_S_W.z],
                    w = targetSectorCostsNew[directions_N_E_S_W.w],
                };
                costs_N_E_S_W += 1f;
                float4 costs_NE_SE_SW_NW = new float4()
                {
                    x = targetSectorCostsNew[directions_NE_SE_SW_NW.x],
                    y = targetSectorCostsNew[directions_NE_SE_SW_NW.y],
                    z = targetSectorCostsNew[directions_NE_SE_SW_NW.z],
                    w = targetSectorCostsNew[directions_NE_SE_SW_NW.w],
                };
                costs_NE_SE_SW_NW += 1.4f;
                float4 min4 = math.min(costs_N_E_S_W, costs_NE_SE_SW_NW);
                return math.min(min4.w, math.min(min4.z, math.min(min4.x, min4.y)));
            }
            void EnqueueNeighbours()
            {
                if (!isBlocked_N_E_S_W.x)
                {
                    fastMarchingQueue.Enqueue(directions_N_E_S_W.x);
                    isBlocked.Set(directions_N_E_S_W.x, true);
                }
                if (!isBlocked_N_E_S_W.y)
                {
                    fastMarchingQueue.Enqueue(directions_N_E_S_W.y);
                    isBlocked.Set(directions_N_E_S_W.y, true);
                }
                if (!isBlocked_N_E_S_W.z)
                {
                    fastMarchingQueue.Enqueue(directions_N_E_S_W.z);
                    isBlocked.Set(directions_N_E_S_W.z, true);
                }
                if (!isBlocked_N_E_S_W.w)
                {
                    fastMarchingQueue.Enqueue(directions_N_E_S_W.w);
                    isBlocked.Set(directions_N_E_S_W.w, true);
                }
            }
        }
    }
}
