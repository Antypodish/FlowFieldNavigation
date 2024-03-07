using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.VisualScripting;


namespace FlowFieldNavigation
{
    [BurstCompile]
    internal struct ActivePortalSubmitJob : IJob
    {
        internal int2 TargetIndex2D;
        internal int SectorTileAmount;
        internal int SectorColAmount;
        internal int SectorRowAmount;
        internal int SectorMatrixColAmount;
        internal int SectorMatrixRowAmount;
        internal int FieldColAmount;

        [ReadOnly] internal NativeArray<PortalNode> PortalNodes;
        [ReadOnly] internal NativeArray<PortalToPortal> PortalEdges;
        [ReadOnly] internal NativeArray<WindowNode> WindowNodes;
        [ReadOnly] internal NativeArray<int> WinToSecPtrs;
        [ReadOnly] internal NativeArray<int> PickedToSector;
        [ReadOnly] internal UnsafeList<int> SectorToPicked;
        [ReadOnly] internal NativeArray<ActivePortal> PortalSequence;
        [ReadOnly] internal NativeArray<int> PortalSequenceBorders;

        internal SectorBitArray SectorBitArray;
        internal NativeArray<UnsafeList<ActiveWaveFront>> ActiveWaveFrontListArray;
        internal NativeList<int> NotActivatedPortals;


        public void Execute()
        {
            for (int i = 0; i < PickedToSector.Length; i++)
            {
                int pickedSector = PickedToSector[i];
                SectorBitArray.SetSector(pickedSector);
            }

            for (int i = 0; i < PortalSequenceBorders.Length - 1; i++)
            {
                int start = PortalSequenceBorders[i];
                int end = PortalSequenceBorders[i + 1];
                //HANDLE PORTALS EXCEPT TARGET NEIGHBOUR
                for (int j = start; j < end - 1; j++)
                {
                    AddCommonSectorsBetweenPortalsToTheWaveFront(j, j + 1);
                }
                ActivePortal endPortal = PortalSequence[end - 1];
                //HANDLE MERGING PORTAL
                if (!endPortal.IsTargetNeighbour())
                {
                    AddCommonSectorsBetweenPortalsToTheWaveFront(end - 1, endPortal.NextIndex);
                }
                //HANDLE TARGET NEIGBOUR POINTING TOWARDS TARGET
                else
                {
                    int endPortalIndex = endPortal.Index;
                    int endPortalWinIndex = PortalNodes[endPortalIndex].WinPtr;
                    WindowNode endPortalWinNode = WindowNodes[endPortalWinIndex];
                    int endSector1 = WinToSecPtrs[endPortalWinNode.WinToSecPtr];
                    int endSector2 = WinToSecPtrs[endPortalWinNode.WinToSecPtr + 1];
                    int2 targetSector2d = FlowFieldUtilities.GetSector2D(TargetIndex2D, SectorColAmount);
                    int targetSector1d = FlowFieldUtilities.To1D(targetSector2d, SectorMatrixColAmount);
                    if (targetSector1d != endSector1 && SectorToPicked[endSector1] != 0)
                    {
                        int pickedSectorIndex = (SectorToPicked[endSector1] - 1) / SectorTileAmount;
                        UnsafeList<ActiveWaveFront> activePortals = ActiveWaveFrontListArray[pickedSectorIndex];
                        int activeLocalIndex = GetIndexOfPortalAtSector(PortalNodes[endPortalIndex], endSector1);
                        ActiveWaveFront newActiveWaveFront = new ActiveWaveFront(activeLocalIndex, endPortal.Distance, end - 1);
                        if (ActiveWaveFrontExists(newActiveWaveFront, activePortals)) { continue; }
                        activePortals.Add(newActiveWaveFront);
                        ActiveWaveFrontListArray[pickedSectorIndex] = activePortals;
                    }
                    else if (targetSector1d != endSector2 && SectorToPicked[endSector2] != 0)
                    {
                        int pickedSectorIndex = (SectorToPicked[endSector2] - 1) / SectorTileAmount;
                        UnsafeList<ActiveWaveFront> activePortals = ActiveWaveFrontListArray[pickedSectorIndex];
                        int activeLocalIndex = GetIndexOfPortalAtSector(PortalNodes[endPortalIndex], endSector2);
                        ActiveWaveFront newActiveWaveFront = new ActiveWaveFront(activeLocalIndex, endPortal.Distance, end - 1);
                        if (ActiveWaveFrontExists(newActiveWaveFront, activePortals)) { continue; }
                        activePortals.Add(newActiveWaveFront);
                        ActiveWaveFrontListArray[pickedSectorIndex] = activePortals;
                    }
                    else
                    {
                        NotActivatedPortals.Add(end - 1);
                    }
                }
            }
            int2 targetSectorIndex2d = FlowFieldUtilities.GetSector2D(TargetIndex2D, SectorColAmount);
            int targetSectorIndex1d = FlowFieldUtilities.To1D(targetSectorIndex2d, SectorMatrixColAmount);
            int targetPickedSectorIndex = (SectorToPicked[targetSectorIndex1d] - 1) / SectorTileAmount;
            int2 targetSectorStartIndex2d = FlowFieldUtilities.GetSectorStartIndex(targetSectorIndex2d, SectorColAmount);
            int2 targetLocalIndex2d = FlowFieldUtilities.GetLocal2D(TargetIndex2D, targetSectorStartIndex2d);
            int targetLocalIndex1d = FlowFieldUtilities.To1D(targetLocalIndex2d, SectorColAmount);
            UnsafeList<ActiveWaveFront> targetActivePortals = ActiveWaveFrontListArray[targetPickedSectorIndex];
            ActiveWaveFront targetFront = new ActiveWaveFront(targetLocalIndex1d, 0f, -1);
            targetActivePortals.Add(targetFront);
            ActiveWaveFrontListArray[targetPickedSectorIndex] = targetActivePortals;
        }

        void AddCommonSectorsBetweenPortalsToTheWaveFront(int curPortalSequenceIndex, int nextPortalSequenceIndex)
        {
            ActivePortal curPortal = PortalSequence[curPortalSequenceIndex];
            ActivePortal nextPortal = PortalSequence[nextPortalSequenceIndex];
            int curPortalIndex = curPortal.Index;
            int nextPortalIndex = nextPortal.Index;
            int windowIndex1 = PortalNodes[curPortalIndex].WinPtr;
            int windowIndex2 = PortalNodes[nextPortalIndex].WinPtr;
            WindowNode winNode1 = WindowNodes[windowIndex1];
            WindowNode winNode2 = WindowNodes[windowIndex2];
            int curSec1Index = WinToSecPtrs[winNode1.WinToSecPtr];
            int curSec2Index = WinToSecPtrs[winNode1.WinToSecPtr + 1];
            int nextSec1Index = WinToSecPtrs[winNode2.WinToSecPtr];
            int nextSec2Index = WinToSecPtrs[winNode2.WinToSecPtr + 1];

            bool sector1Common = (curSec1Index == nextSec1Index || curSec1Index == nextSec2Index);
            bool sector2Common = (curSec2Index == nextSec1Index || curSec2Index == nextSec2Index);
            bool sector1Included = SectorToPicked[curSec1Index] != 0;
            bool sector2Included = SectorToPicked[curSec2Index] != 0;
            if (!sector1Common && sector1Included)
            {
                int pickedSectorIndex = (SectorToPicked[curSec1Index] - 1) / SectorTileAmount;
                UnsafeList<ActiveWaveFront> activePortals = ActiveWaveFrontListArray[pickedSectorIndex];
                int activeLocalIndex = GetIndexOfPortalAtSector(PortalNodes[curPortalIndex], curSec1Index);
                ActiveWaveFront newActiveWaveFront = new ActiveWaveFront(activeLocalIndex, curPortal.Distance, curPortalSequenceIndex);
                activePortals.Add(newActiveWaveFront);
                ActiveWaveFrontListArray[pickedSectorIndex] = activePortals;

            }
            else if (!sector2Common && sector2Included)
            {
                int pickedSectorIndex = (SectorToPicked[curSec2Index] - 1) / SectorTileAmount;
                UnsafeList<ActiveWaveFront> activePortals = ActiveWaveFrontListArray[pickedSectorIndex];
                int activeLocalIndex = GetIndexOfPortalAtSector(PortalNodes[curPortalIndex], curSec2Index);
                ActiveWaveFront newActiveWaveFront = new ActiveWaveFront(activeLocalIndex, curPortal.Distance, curPortalSequenceIndex);
                activePortals.Add(newActiveWaveFront);
                ActiveWaveFrontListArray[pickedSectorIndex] = activePortals;
            }
            else if (sector1Common && sector2Common && sector1Included && sector2Included)
            {
                PortalNode node1 = PortalNodes[curPortalIndex];
                Portal n1p1 = node1.Portal1;
                Portal n1p2 = node1.Portal2;
                if (!IsConnected(n1p1, nextPortalIndex))
                {
                    int2 n1p1index = new int2(n1p1.Index.C, n1p1.Index.R);
                    int sector1d = FlowFieldUtilities.GetSector1D(n1p1index, SectorColAmount, SectorMatrixColAmount);
                    int pickedSectorIndex = (SectorToPicked[sector1d] - 1) / SectorTileAmount;
                    UnsafeList<ActiveWaveFront> activePortals = ActiveWaveFrontListArray[pickedSectorIndex];
                    int activeLocalIndex = GetIndexOfPortalAtSector(PortalNodes[curPortalIndex], sector1d);
                    ActiveWaveFront newActiveWaveFront = new ActiveWaveFront(activeLocalIndex, curPortal.Distance, curPortalSequenceIndex);
                    activePortals.Add(newActiveWaveFront);
                    ActiveWaveFrontListArray[pickedSectorIndex] = activePortals;
                }
                else
                {
                    int2 n1p2index = new int2(n1p2.Index.C, n1p2.Index.R);
                    int sector1d = FlowFieldUtilities.GetSector1D(n1p2index, SectorColAmount, SectorMatrixColAmount);
                    int pickedSectorIndex = (SectorToPicked[sector1d] - 1) / SectorTileAmount;
                    UnsafeList<ActiveWaveFront> activePortals = ActiveWaveFrontListArray[pickedSectorIndex];
                    int activeLocalIndex = GetIndexOfPortalAtSector(PortalNodes[curPortalIndex], sector1d);
                    ActiveWaveFront newActiveWaveFront = new ActiveWaveFront(activeLocalIndex, curPortal.Distance, curPortalSequenceIndex);
                    activePortals.Add(newActiveWaveFront);
                    ActiveWaveFrontListArray[pickedSectorIndex] = activePortals;
                }
            }
            else
            {
                NotActivatedPortals.Add(curPortalSequenceIndex);
            }
        }

        bool IsConnected(Portal portal, int portalNodeIndex)
        {
            //BRANCHLESS :)))
            int porEdgeStart = portal.PorToPorPtr;
            int porEdgeCount = portal.PorToPorCnt;
            bool connected = false;
            for (int i = porEdgeStart; i < porEdgeStart + porEdgeCount; i++)
            {
                connected = connected || PortalEdges[i].Index == portalNodeIndex;
            }
            return connected;
        }
        bool ActiveWaveFrontExists(ActiveWaveFront front, UnsafeList<ActiveWaveFront> list)
        {
            for (int i = 0; i < list.Length; i++)
            {
                ActiveWaveFront curFront = list[i];
                if (curFront.LocalIndex == front.LocalIndex) { return true; }
            }
            return false;
        }
        int GetIndexOfPortalAtSector(PortalNode portalNode, int sectorIndex)
        {
            int2 i1 = new int2(portalNode.Portal1.Index.C, portalNode.Portal1.Index.R);
            int2 i2 = new int2(portalNode.Portal2.Index.C, portalNode.Portal2.Index.R);
            int2 i1sector2d = FlowFieldUtilities.GetSector2D(i1, SectorColAmount);
            int2 i2sector2d = FlowFieldUtilities.GetSector2D(i2, SectorColAmount);

            int i1sector1d = FlowFieldUtilities.To1D(i1sector2d, SectorMatrixColAmount);

            int2 pickedIndex2d = math.select(i2, i1, i1sector1d == sectorIndex);
            int2 i1sectorStart2d = FlowFieldUtilities.GetSectorStartIndex(i1sector2d, SectorColAmount);
            int2 i2sectorStart2d = FlowFieldUtilities.GetSectorStartIndex(i2sector2d, SectorColAmount);
            int2 pickedSectorStart2d = math.select(i2sectorStart2d, i1sectorStart2d, i1sector1d == sectorIndex);

            int2 pickedIndexLocal2d = FlowFieldUtilities.GetLocal2D(pickedIndex2d, pickedSectorStart2d);
            return FlowFieldUtilities.To1D(pickedIndexLocal2d, SectorColAmount);
        }
    }
    [BurstCompile]
    internal struct ActiveWaveFront
    {
        internal int LocalIndex;
        internal float Distance;
        internal int PortalSequenceIndex;

        internal ActiveWaveFront(int localIndes, float distance, int portalSequenceIndex)
        {
            LocalIndex = localIndes;
            Distance = distance;
            PortalSequenceIndex = portalSequenceIndex;
        }
        internal void SetTarget()
        {
            PortalSequenceIndex = -1;
        }
        internal bool IsTarget()
        {
            return PortalSequenceIndex == -1;
        }
    }   

}