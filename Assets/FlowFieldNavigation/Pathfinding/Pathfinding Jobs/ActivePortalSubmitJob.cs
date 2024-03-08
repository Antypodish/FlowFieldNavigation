using Unity.Collections;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;
using Unity.Mathematics;

namespace FlowFieldNavigation
{
    [BurstCompile]
    internal struct ActivePortalSubmitJob:IJob
    {
        internal int SequenceBorderListStartIndex;
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
        [ReadOnly] internal NativeReference<int> NewSectorStartIndex;

        internal SectorBitArray SectorBitArray;
        internal NativeArray<UnsafeList<ActiveWaveFront>> ActiveWaveFrontListArray;
        internal NativeList<int> NotActivatedPortals;
        internal UnsafeList<PathSectorState> SectorStateTable;

        public void Execute()
        {
            for (int i = NewSectorStartIndex.Value; i < PickedToSector.Length; i++)
            {
                int pickedSector = PickedToSector[i];
                SectorToPicked[pickedSector] = i * SectorTileAmount + 1;
                SectorBitArray.SetSector(pickedSector);
            }

            for (int i = SequenceBorderListStartIndex; i < PortalSequenceBorders.Length - 1; i++)
            {
                int start = PortalSequenceBorders[i];
                int end = PortalSequenceBorders[i + 1];
                //HANDLE PORTALS EXCEPT TARGET NEIGHBOUR
                for (int j = start; j < end - 1; j++)
                {
                    bool succesfull = AddCommonSectorsBetweenPortalsToTheWaveFront(j, j + 1);
                    if (!succesfull) { NotActivatedPortals.Add(j); }
                }
                ActivePortal endPortal = PortalSequence[end - 1];
                //HANDLE MERGING PORTAL
                if (!endPortal.IsTargetNeighbour())
                {
                    bool succesfull = AddCommonSectorsBetweenPortalsToTheWaveFront(end - 1, endPortal.NextIndex);
                    if (!succesfull) { NotActivatedPortals.Add(end - 1); }
                }
                //HANDLE TARGET NEIGBOUR POINTING TOWARDS TARGET
                else
                {
                    int endSector1 = FlowFieldUtilities.GetSector1D(endPortal.FieldIndex1, FieldColAmount, SectorColAmount, SectorMatrixColAmount);
                    int endSector2 = FlowFieldUtilities.GetSector1D(endPortal.FieldIndex2, FieldColAmount, SectorColAmount, SectorMatrixColAmount);
                    int2 targetSector2d = FlowFieldUtilities.GetSector2D(TargetIndex2D, SectorColAmount);
                    int targetSector1d = FlowFieldUtilities.To1D(targetSector2d, SectorMatrixColAmount);
                    if (targetSector1d != endSector1 && SectorToPicked[endSector1] != 0)
                    {
                        int pickedSectorIndex = (SectorToPicked[endSector1] - 1) / SectorTileAmount;
                        UnsafeList<ActiveWaveFront> activePortals = ActiveWaveFrontListArray[pickedSectorIndex];
                        int activeLocalIndex = PickIndexAtSectorAsLocalIndex(endPortal.FieldIndex1, endPortal.FieldIndex2, endSector1);
                        ActiveWaveFront newActiveWaveFront = new ActiveWaveFront(activeLocalIndex, endPortal.Distance, end - 1);
                        if (ActiveWaveFrontExists(newActiveWaveFront, activePortals)) { continue; }
                        activePortals.Add(newActiveWaveFront);
                        ActiveWaveFrontListArray[pickedSectorIndex] = activePortals;
                        PathSectorState sectorState = SectorStateTable[endSector1];
                        sectorState = ~((~sectorState) | PathSectorState.IntegrationCalculated | PathSectorState.FlowCalculated);
                        SectorStateTable[endSector1] = sectorState;
                    }
                    else if (targetSector1d != endSector2 && SectorToPicked[endSector2] != 0)
                    {
                        int pickedSectorIndex = (SectorToPicked[endSector2] - 1) / SectorTileAmount;
                        UnsafeList<ActiveWaveFront> activePortals = ActiveWaveFrontListArray[pickedSectorIndex];
                        int activeLocalIndex = PickIndexAtSectorAsLocalIndex(endPortal.FieldIndex1, endPortal.FieldIndex2, endSector2);
                        ActiveWaveFront newActiveWaveFront = new ActiveWaveFront(activeLocalIndex, endPortal.Distance, end - 1);
                        if (ActiveWaveFrontExists(newActiveWaveFront, activePortals)) { continue; }
                        activePortals.Add(newActiveWaveFront);
                        ActiveWaveFrontListArray[pickedSectorIndex] = activePortals;
                        PathSectorState sectorState = SectorStateTable[endSector2];
                        sectorState = ~((~sectorState) | PathSectorState.IntegrationCalculated | PathSectorState.FlowCalculated);
                        SectorStateTable[endSector2] = sectorState;
                    }
                    else
                    {
                        NotActivatedPortals.Add(end - 1);
                    }
                }
            }
            for (int i = NotActivatedPortals.Length - 1; i >= 0; i--)
            {
                int seqIndex1 = NotActivatedPortals[i];
                ActivePortal endPortal = PortalSequence[seqIndex1];
                if (!endPortal.IsTargetNeighbour())
                {
                    bool succesfull = AddCommonSectorsBetweenPortalsToTheWaveFront(seqIndex1, endPortal.NextIndex);
                    if (succesfull) { NotActivatedPortals.RemoveAtSwapBack(i); }
                }
                else
                {
                    int endSector1 = FlowFieldUtilities.GetSector1D(endPortal.FieldIndex1, FieldColAmount, SectorColAmount, SectorMatrixColAmount);
                    int endSector2 = FlowFieldUtilities.GetSector1D(endPortal.FieldIndex2, FieldColAmount, SectorColAmount, SectorMatrixColAmount);
                    int2 targetSector2d = FlowFieldUtilities.GetSector2D(TargetIndex2D, SectorColAmount);
                    int targetSector1d = FlowFieldUtilities.To1D(targetSector2d, SectorMatrixColAmount);
                    if (targetSector1d != endSector1 && SectorToPicked[endSector1] != 0)
                    {
                        int pickedSectorIndex = (SectorToPicked[endSector1] - 1) / SectorTileAmount;
                        UnsafeList<ActiveWaveFront> activePortals = ActiveWaveFrontListArray[pickedSectorIndex];
                        int activeLocalIndex = PickIndexAtSectorAsLocalIndex(endPortal.FieldIndex1, endPortal.FieldIndex2, endSector1);
                        ActiveWaveFront newActiveWaveFront = new ActiveWaveFront(activeLocalIndex, endPortal.Distance, seqIndex1 - 1);
                        if (ActiveWaveFrontExists(newActiveWaveFront, activePortals)) { continue; }
                        activePortals.Add(newActiveWaveFront);
                        ActiveWaveFrontListArray[pickedSectorIndex] = activePortals;
                        PathSectorState sectorState = SectorStateTable[endSector1];
                        sectorState = ~((~sectorState) | PathSectorState.IntegrationCalculated | PathSectorState.FlowCalculated);
                        SectorStateTable[endSector1] = sectorState;
                        NotActivatedPortals.RemoveAtSwapBack(i);
                    }
                    else if (targetSector1d != endSector2 && SectorToPicked[endSector2] != 0)
                    {
                        int pickedSectorIndex = (SectorToPicked[endSector2] - 1) / SectorTileAmount;
                        UnsafeList<ActiveWaveFront> activePortals = ActiveWaveFrontListArray[pickedSectorIndex];
                        int activeLocalIndex = PickIndexAtSectorAsLocalIndex(endPortal.FieldIndex1, endPortal.FieldIndex2, endSector2);
                        ActiveWaveFront newActiveWaveFront = new ActiveWaveFront(activeLocalIndex, endPortal.Distance, seqIndex1 - 1);
                        if (ActiveWaveFrontExists(newActiveWaveFront, activePortals)) { continue; }
                        activePortals.Add(newActiveWaveFront);
                        ActiveWaveFrontListArray[pickedSectorIndex] = activePortals;
                        PathSectorState sectorState = SectorStateTable[endSector2];
                        sectorState = ~((~sectorState) | PathSectorState.IntegrationCalculated | PathSectorState.FlowCalculated);
                        SectorStateTable[endSector2] = sectorState;
                        NotActivatedPortals.RemoveAtSwapBack(i);
                    }
                }

            }
            
            int2 targetSectorIndex2d = FlowFieldUtilities.GetSector2D(TargetIndex2D, SectorColAmount);
            int targetSectorIndex1d = FlowFieldUtilities.To1D(targetSectorIndex2d, SectorMatrixColAmount);
            int targetPickedSectorIndex = (SectorToPicked[targetSectorIndex1d] - 1) / SectorTileAmount;
            int2 targetSectorStartIndex2d = FlowFieldUtilities.GetSectorStartIndex(targetSectorIndex2d, SectorColAmount);
            int2 targetLocalIndex2d = FlowFieldUtilities.GetLocal2D(TargetIndex2D, targetSectorStartIndex2d);
            int targetLocalIndex1d = FlowFieldUtilities.To1D(targetLocalIndex2d, SectorColAmount);
            ActiveWaveFront targetFront = new ActiveWaveFront(targetLocalIndex1d, 0f, -1);
            UnsafeList<ActiveWaveFront> targetActivePortals = ActiveWaveFrontListArray[targetPickedSectorIndex];
            if (!ActiveWaveFrontExists(targetFront, targetActivePortals))
            {
                targetActivePortals.Add(targetFront);
                ActiveWaveFrontListArray[targetPickedSectorIndex] = targetActivePortals;
            }
        }
        bool AddCommonSectorsBetweenPortalsToTheWaveFront(int curPortalSequenceIndex, int nextPortalSequenceIndex)
        {
            ActivePortal curPortal = PortalSequence[curPortalSequenceIndex];
            ActivePortal nextPortal = PortalSequence[nextPortalSequenceIndex];
            (int curSec1Index, int curSec2Index, int nextSec1Index, int nextSec2Index) = GetSectorsOfPortals(curPortal, nextPortal);

            bool sector1Common = (curSec1Index == nextSec1Index || curSec1Index == nextSec2Index);
            bool sector2Common = (curSec2Index == nextSec1Index || curSec2Index == nextSec2Index);
            bool bothCommon = sector1Common && sector2Common;
            bool sector1Included = SectorToPicked[curSec1Index] != 0;
            bool sector2Included = SectorToPicked[curSec2Index] != 0;
            bool bothIncluded = sector1Included && sector2Included;

            bool succesfull = false;
            if ((!sector1Common && sector1Included) || (bothIncluded && bothCommon))
            {
                int pickedSectorIndex = (SectorToPicked[curSec1Index] - 1) / SectorTileAmount;
                UnsafeList<ActiveWaveFront> activePortals = ActiveWaveFrontListArray[pickedSectorIndex];
                int activeLocalIndex = PickIndexAtSectorAsLocalIndex(curPortal.FieldIndex1, curPortal.FieldIndex2, curSec1Index);
                ActiveWaveFront newActiveWaveFront = new ActiveWaveFront(activeLocalIndex, curPortal.Distance, curPortalSequenceIndex);
                activePortals.Add(newActiveWaveFront);
                ActiveWaveFrontListArray[pickedSectorIndex] = activePortals;
                PathSectorState sectorState = SectorStateTable[curSec1Index];
                sectorState = ~((~sectorState) | PathSectorState.IntegrationCalculated | PathSectorState.FlowCalculated);
                SectorStateTable[curSec1Index] = sectorState;
                succesfull = true;
            }
            if ((!sector2Common && sector2Included) || (bothIncluded && bothCommon))
            {
                int pickedSectorIndex = (SectorToPicked[curSec2Index] - 1) / SectorTileAmount;
                UnsafeList<ActiveWaveFront> activePortals = ActiveWaveFrontListArray[pickedSectorIndex];
                int activeLocalIndex = PickIndexAtSectorAsLocalIndex(curPortal.FieldIndex1, curPortal.FieldIndex2, curSec2Index);
                ActiveWaveFront newActiveWaveFront = new ActiveWaveFront(activeLocalIndex, curPortal.Distance, curPortalSequenceIndex);
                activePortals.Add(newActiveWaveFront);
                ActiveWaveFrontListArray[pickedSectorIndex] = activePortals;
                PathSectorState sectorState = SectorStateTable[curSec2Index];
                sectorState = ~((~sectorState) | PathSectorState.IntegrationCalculated | PathSectorState.FlowCalculated);
                SectorStateTable[curSec2Index] = sectorState;
                succesfull = true;
            }
            return succesfull;
        }
        int PickIndexAtSectorAsLocalIndex(int index1, int index2, int sectorIndex)
        {
            int2 i1 = FlowFieldUtilities.To2D(index1, FieldColAmount);
            int2 i2 = FlowFieldUtilities.To2D(index2, FieldColAmount);
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
        bool ActiveWaveFrontExists(ActiveWaveFront front, UnsafeList<ActiveWaveFront> list)
        {
            for (int i = 0; i < list.Length; i++)
            {
                ActiveWaveFront curFront = list[i];
                if (curFront.LocalIndex == front.LocalIndex) { return true; }
            }
            return false;
        }
        (int p1Sec1, int p1Sec2, int p2Sec1, int p2Sec2) GetSectorsOfPortals(ActivePortal portal1, ActivePortal portal2)
        {
            int p1Sec1 = FlowFieldUtilities.GetSector1D(portal1.FieldIndex1, FieldColAmount, SectorColAmount, SectorMatrixColAmount);
            int p1Sec2 = FlowFieldUtilities.GetSector1D(portal1.FieldIndex2, FieldColAmount, SectorColAmount, SectorMatrixColAmount);
            int p2Sec1 = FlowFieldUtilities.GetSector1D(portal2.FieldIndex1, FieldColAmount, SectorColAmount, SectorMatrixColAmount);
            int p2Sec2 = FlowFieldUtilities.GetSector1D(portal2.FieldIndex2, FieldColAmount, SectorColAmount, SectorMatrixColAmount);
            return (p1Sec1, p1Sec2, p2Sec1, p2Sec2);
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
