using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.XR;

namespace FlowFieldNavigation
{
    [BurstCompile]
    internal struct PortalTraversalJob : IJob
    {
        internal int2 TargetIndex;
        internal int SectorColAmount;
        internal int SectorMatrixColAmount;

        internal NativeArray<PortalTraversalData> PortalTraversalDataArray;
        internal NativeList<ActivePortal> PortalSequence;

        internal NativeList<int> PortalSequenceBorders;
        internal UnsafeList<int> SectorToPicked;
        internal UnsafeList<PathSectorState> SectorStateTable;
        internal NativeList<int> PickedToSector;
        internal NativeReference<int> FlowFieldLength;
        internal NativeList<int> SourcePortals;

        [ReadOnly] internal NativeArray<SectorNode> SectorNodes;
        [ReadOnly] internal NativeArray<WindowNode> WindowNodes;
        [ReadOnly] internal NativeArray<int> WinToSecPtrs;
        [ReadOnly] internal NativeArray<PortalNode> PortalNodes;
        [ReadOnly] internal NativeArray<PortalToPortal> PorPtrs;

        internal NativeList<int> TargetNeighbourPortalIndicies;

        int _targetSectorIndex1d;
        public void Execute()
        {
            //TARGET DATA
            int2 targetSectorIndex2d = new int2(TargetIndex.x / SectorColAmount, TargetIndex.y / SectorColAmount);
            _targetSectorIndex1d = targetSectorIndex2d.y * SectorMatrixColAmount + targetSectorIndex2d.x;
            if (TargetNeighbourPortalIndicies.Length == 0)
            {
                AddTargetSector();
                return;
            }
            //START GRAPH WALKER
            PortalSequenceBorders.Add(0);
            RunDijkstra();
            NativeArray<int> sourcePortalsAsArray = SourcePortals.AsArray();
            for (int i = 0; i < sourcePortalsAsArray.Length; i++)
            {
                PickPortalSequenceFromDijkstra(sourcePortalsAsArray[i]);
            }
            PickSectorsFromPortalSequence();
            AddTargetSector();
        }
        void PickPortalSequenceFromDijkstra(int sourcePortal)
        {
            //NOTE: NextIndex of portalTraversalData is used as:
            //1. NextIndex in portalTraversalDataArray
            //2. PortalSequence of corresponding portalTraversalData
            //For memory optimization reasons :/

            PortalTraversalData sourceData = PortalTraversalDataArray[sourcePortal];

            if (sourceData.HasMark(PortalTraversalMark.DijkstraPicked)) { return; }

            int nextDataIndex = sourceData.NextIndex;
            sourceData.Mark |= PortalTraversalMark.DijkstraPicked;
            sourceData.NextIndex = PortalSequence.Length;
            PortalTraversalDataArray[sourcePortal] = sourceData;

            ActivePortal sourceActivePortal = new ActivePortal()
            {
                Index = sourcePortal,
                Distance = sourceData.DistanceFromTarget,
                NextIndex = PortalSequence.Length + 1,
            };

            //IF SOURCE IS TARGET NEIGHBOUR
            if (nextDataIndex == -1)
            {
                sourceActivePortal.NextIndex = -1;
                PortalSequence.Add(sourceActivePortal);
                PortalSequenceBorders.Add(PortalSequence.Length);
                return;
            }

            //IF SOURCE IS NOT TARGET NEIGHBOUR
            PortalSequence.Add(sourceActivePortal);
            int curIndex = nextDataIndex;

            while (curIndex != -1)
            {
                PortalTraversalData curData = PortalTraversalDataArray[curIndex];
                if (curData.HasMark(PortalTraversalMark.DijkstraPicked))
                {
                    ActivePortal previousNode = PortalSequence[PortalSequence.Length - 1];
                    previousNode.NextIndex = curData.NextIndex;
                    PortalSequence[PortalSequence.Length - 1] = previousNode;
                    break;
                }
                //PUSH ACTIVE PORTAL
                ActivePortal curActivePortal = new ActivePortal()
                {
                    Index = curIndex,
                    Distance = curData.DistanceFromTarget,
                    NextIndex = math.select(PortalSequence.Length + 1, -1, curData.NextIndex == -1),
                };
                PortalSequence.Add(curActivePortal);

                //MARK OR STOP
                int curDataIndex = curData.NextIndex;
                curData.Mark |= PortalTraversalMark.DijkstraPicked;
                curData.NextIndex = PortalSequence.Length - 1;
                PortalTraversalDataArray[curIndex] = curData;

                curIndex = curDataIndex;
            }
            PortalSequenceBorders.Add(PortalSequence.Length);
        }
        void RunDijkstra()
        {
            SingleFloatUnsafeHeap<int> travHeap = new SingleFloatUnsafeHeap<int>(0, Allocator.Temp);
            NativeArray<PortalTraversalData> portalTraversalDataArray = PortalTraversalDataArray;
            NativeArray<PortalNode> portalNodes = PortalNodes;
            NativeArray<PortalToPortal> porPtrs = PorPtrs;

            //MARK TARGET NEIGHBOURS
            for (int i = 0; i < TargetNeighbourPortalIndicies.Length; i++)
            {
                int index = TargetNeighbourPortalIndicies[i];
                PortalTraversalData targetNeighbour = PortalTraversalDataArray[index];
                targetNeighbour.Mark |= PortalTraversalMark.DijkstraTraversed;
                targetNeighbour.DistanceFromTarget++;
                PortalTraversalDataArray[index] = targetNeighbour;
            }

            for (int i = 0; i < TargetNeighbourPortalIndicies.Length; i++)
            {
                int index = TargetNeighbourPortalIndicies[i];
                float distanceFromTarget = portalTraversalDataArray[index].DistanceFromTarget;
                EnqueueNeighbours(index, distanceFromTarget);
            }

            int curIndex = GetNextIndex();
            while (curIndex != -1)
            {
                float distanceFromTarget = portalTraversalDataArray[curIndex].DistanceFromTarget;
                EnqueueNeighbours(curIndex, distanceFromTarget);
                curIndex = GetNextIndex();
            }
            int GetNextIndex()
            {
                if (travHeap.IsEmpty) { return -1; }
                int nextMinIndex = travHeap.ExtractMin();
                PortalTraversalData nextMinTraversalData = portalTraversalDataArray[nextMinIndex];
                while (nextMinTraversalData.HasMark(PortalTraversalMark.DijstraExtracted))
                {
                    if (travHeap.IsEmpty) { return -1; }
                    nextMinIndex = travHeap.ExtractMin();
                    nextMinTraversalData = portalTraversalDataArray[nextMinIndex];
                }
                nextMinTraversalData.Mark |= PortalTraversalMark.DijstraExtracted;
                portalTraversalDataArray[nextMinIndex] = nextMinTraversalData;
                return nextMinIndex;
            }
            void EnqueueNeighbours(int index, float gCost)
            {
                PortalNode portal = portalNodes[index];
                int por1Ptr = portal.Portal1.PorToPorPtr;
                int por1Cnt = portal.Portal1.PorToPorCnt;
                int por2Ptr = portal.Portal2.PorToPorPtr;
                int por2Cnt = portal.Portal2.PorToPorCnt;

                for (int i = por1Ptr; i < por1Ptr + por1Cnt; i++)
                {
                    int portalIndex = porPtrs[i].Index;
                    float portalDistance = porPtrs[i].Distance;
                    PortalTraversalData porData = portalTraversalDataArray[portalIndex];
                    if (!porData.HasMark(PortalTraversalMark.Reduced) || porData.HasMark(PortalTraversalMark.DijstraExtracted)) { continue; }
                    if (porData.HasMark(PortalTraversalMark.DijkstraTraversed))
                    {
                        float newGCost = gCost + portalDistance + 1;
                        if (porData.DistanceFromTarget <= newGCost) { continue; }
                        porData.DistanceFromTarget = newGCost;
                        porData.NextIndex = index;
                        portalTraversalDataArray[portalIndex] = porData;
                        travHeap.Add(portalIndex, newGCost);
                        continue;
                    }
                    porData.Mark |= PortalTraversalMark.DijkstraTraversed;
                    porData.DistanceFromTarget = gCost + portalDistance + 1;
                    porData.NextIndex = index;
                    portalTraversalDataArray[portalIndex] = porData;
                    travHeap.Add(portalIndex, gCost + portalDistance + 1);
                }
                for (int i = por2Ptr; i < por2Ptr + por2Cnt; i++)
                {
                    int portalIndex = porPtrs[i].Index;
                    float portalDistance = porPtrs[i].Distance;
                    PortalTraversalData porData = portalTraversalDataArray[portalIndex];
                    if (!porData.HasMark(PortalTraversalMark.Reduced) || porData.HasMark(PortalTraversalMark.DijstraExtracted)) { continue; }
                    if (porData.HasMark(PortalTraversalMark.DijkstraTraversed))
                    {
                        float newGCost = gCost + portalDistance + 1;
                        if (porData.DistanceFromTarget <= newGCost) { continue; }
                        porData.DistanceFromTarget = newGCost;
                        porData.NextIndex = index;
                        portalTraversalDataArray[portalIndex] = porData;
                        travHeap.Add(portalIndex, newGCost);
                        continue;
                    }
                    porData.Mark |= PortalTraversalMark.DijkstraTraversed;
                    porData.DistanceFromTarget = gCost + portalDistance + 1;
                    porData.NextIndex = index;
                    portalTraversalDataArray[portalIndex] = porData;
                    travHeap.Add(portalIndex, gCost + portalDistance + 1);
                }
            }
        }
        void PickSectorsFromPortalSequence()
        {
            for (int i = 0; i < PortalSequenceBorders.Length - 1; i++)
            {
                int start = PortalSequenceBorders[i];
                int end = PortalSequenceBorders[i + 1];
                for (int j = start; j < end - 1; j++)
                {
                    int portalIndex1 = PortalSequence[j].Index;
                    int portalIndex2 = PortalSequence[j + 1].Index;
                    PickSectorsBetweenportals(portalIndex1, portalIndex2);
                }
                ActivePortal lastActivePortalInBorder = PortalSequence[end - 1];
                if (lastActivePortalInBorder.NextIndex != -1)
                {
                    int portalIndex1 = lastActivePortalInBorder.Index;
                    int portalIndex2 = PortalSequence[lastActivePortalInBorder.NextIndex].Index;
                    PickSectorsBetweenportals(portalIndex1, portalIndex2);
                }

            }
        }
        void PickSectorsBetweenportals(int portalIndex1, int portalIndex2)
        {
            int sectorTileAmount = SectorColAmount * SectorColAmount;
            int windowIndex1 = PortalNodes[portalIndex1].WinPtr;
            int windowIndex2 = PortalNodes[portalIndex2].WinPtr;
            WindowNode winNode1 = WindowNodes[windowIndex1];
            WindowNode winNode2 = WindowNodes[windowIndex2];
            int win1Sec1Index = WinToSecPtrs[winNode1.WinToSecPtr];
            int win1Sec2Index = WinToSecPtrs[winNode1.WinToSecPtr + 1];
            int win2Sec1Index = WinToSecPtrs[winNode2.WinToSecPtr];
            int win2Sec2Index = WinToSecPtrs[winNode2.WinToSecPtr + 1];
            if ((win1Sec1Index == win2Sec1Index || win1Sec1Index == win2Sec2Index) && SectorToPicked[win1Sec1Index] == 0)
            {
                SectorToPicked[win1Sec1Index] = PickedToSector.Length * sectorTileAmount + 1;
                PickedToSector.Add(win1Sec1Index);
                SectorStateTable[win1Sec1Index] |= PathSectorState.Included;
            }
            if ((win1Sec2Index == win2Sec1Index || win1Sec2Index == win2Sec2Index) && SectorToPicked[win1Sec2Index] == 0)
            {
                SectorToPicked[win1Sec2Index] = PickedToSector.Length * sectorTileAmount + 1;
                PickedToSector.Add(win1Sec2Index);
                SectorStateTable[win1Sec2Index] |= PathSectorState.Included;
            }
        }
        void AddTargetSector()
        {
            int sectorTileAmount = SectorColAmount * SectorColAmount;
            if (SectorToPicked[_targetSectorIndex1d] == 0)
            {
                SectorToPicked[_targetSectorIndex1d] = PickedToSector.Length * sectorTileAmount + 1;
                PickedToSector.Add(_targetSectorIndex1d);
                SectorStateTable[_targetSectorIndex1d] |= PathSectorState.Included;
            }
            FlowFieldLength.Value = PickedToSector.Length * sectorTileAmount + 1;
        }
    }
}

