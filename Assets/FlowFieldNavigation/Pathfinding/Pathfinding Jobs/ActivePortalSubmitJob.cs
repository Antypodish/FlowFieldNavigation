using Unity.Collections;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;
using Unity.Mathematics;
using System;

namespace FlowFieldNavigation
{
    [BurstCompile]
    internal struct ActivePortalSubmitJob:IJob
    {
        internal int SequenceSliceListStartIndex;
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
        [ReadOnly] internal NativeList<int> PickedSectorIndicies;
        [ReadOnly] internal NativeList<ActivePortal> PortalSequence;
        [ReadOnly] internal NativeList<Slice> PortalSequenceSlices;
        [ReadOnly] internal NativeReference<int> NewSectorStartIndex;

        internal UnsafeList<int> SectorToPicked;
        internal SectorBitArray SectorBitArray;
        internal NativeParallelMultiHashMap<int, ActiveWaveFront> SectorToWaveFrontsMap;
        internal NativeList<NotActivePortalRecord> NotActivatedPortals;
        internal UnsafeList<PathSectorState> SectorStateTable;
        internal NativeArray<OverlappingDirection> SectorOverlappingDirectionTable;

        public void Execute()
        {
            NativeArray<int> PickedSectorIndiciesAsArray = PickedSectorIndicies.AsArray();
            NativeArray<ActivePortal> PortalSequenceAsArray = PortalSequence.AsArray();
            NativeArray<Slice> PortalSequenceSlicesAsArray = PortalSequenceSlices.AsArray();
            for (int i = NewSectorStartIndex.Value; i < PickedSectorIndiciesAsArray.Length; i++)
            {
                int pickedSector = PickedSectorIndiciesAsArray[i];
                SectorToPicked[pickedSector] = i * SectorTileAmount + 1;
                SectorBitArray.SetSector(pickedSector);
            }

            for (int i = SequenceSliceListStartIndex; i < PortalSequenceSlicesAsArray.Length; i++)
            {
                Slice slice = PortalSequenceSlicesAsArray[i];
                int start = slice.Index;
                int end = slice.Index + slice.Count;

                for (int j = start; j < end - 1; j++)
                {
                    bool succesfull = AddCommonSectorsBetweenPortalsToTheWaveFront(j, j + 1, PortalSequenceAsArray);
                    if (!succesfull) { NotActivatedPortals.Add(new NotActivePortalRecord(j, j+1)); }
                }
            }
            for (int i = NotActivatedPortals.Length - 1; i >= 0; i--)
            {
                NotActivePortalRecord record = NotActivatedPortals[i];
                int curActivePortalIndex = record.CurSequenceIndex;
                int nextActivePortalIndex = record.NextSequenceIndex;
                bool succesfull = AddCommonSectorsBetweenPortalsToTheWaveFront(curActivePortalIndex, nextActivePortalIndex, PortalSequenceAsArray);
                if (succesfull) { NotActivatedPortals.RemoveAtSwapBack(i); }
            }
        }
        bool AddCommonSectorsBetweenPortalsToTheWaveFront(int curPortalSequenceIndex, int nextPortalSequenceIndex, NativeArray<ActivePortal> portalSequenceAsArray)
        {
            ActivePortal curPortal = portalSequenceAsArray[curPortalSequenceIndex];
            ActivePortal nextPortal = portalSequenceAsArray[nextPortalSequenceIndex];
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
                int activeLocalIndex = PickIndexAtSectorAsLocalIndex(curPortal.FieldIndex1, curPortal.FieldIndex2, curSec1Index);
                ActiveWaveFront newActiveWaveFront = new ActiveWaveFront(activeLocalIndex, curPortal.Distance); 
                SectorToWaveFrontsMap.Add(curSec1Index, newActiveWaveFront);
                PathSectorState sectorState = SectorStateTable[curSec1Index];
                sectorState = ~((~sectorState) | PathSectorState.IntegrationCalculated | PathSectorState.FlowCalculated);
                SectorStateTable[curSec1Index] = sectorState;
                SubmitOverlappingSectors(curSec1Index, curSec2Index);
                succesfull = true;
            }
            if ((!sector2Common && sector2Included) || (bothIncluded && bothCommon))
            {
                int activeLocalIndex = PickIndexAtSectorAsLocalIndex(curPortal.FieldIndex1, curPortal.FieldIndex2, curSec2Index);
                ActiveWaveFront newActiveWaveFront = new ActiveWaveFront(activeLocalIndex, curPortal.Distance);
                SectorToWaveFrontsMap.Add(curSec2Index, newActiveWaveFront);
                PathSectorState sectorState = SectorStateTable[curSec2Index];
                sectorState = ~((~sectorState) | PathSectorState.IntegrationCalculated | PathSectorState.FlowCalculated);
                SectorStateTable[curSec2Index] = sectorState;
                SubmitOverlappingSectors(curSec2Index, curSec1Index);
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
        (int p1Sec1, int p1Sec2, int p2Sec1, int p2Sec2) GetSectorsOfPortals(ActivePortal portal1, ActivePortal portal2)
        {
            int p1Sec1 = FlowFieldUtilities.GetSector1D(portal1.FieldIndex1, FieldColAmount, SectorColAmount, SectorMatrixColAmount);
            int p1Sec2 = FlowFieldUtilities.GetSector1D(portal1.FieldIndex2, FieldColAmount, SectorColAmount, SectorMatrixColAmount);
            int p2Sec1 = FlowFieldUtilities.GetSector1D(portal2.FieldIndex1, FieldColAmount, SectorColAmount, SectorMatrixColAmount);
            int p2Sec2 = FlowFieldUtilities.GetSector1D(portal2.FieldIndex2, FieldColAmount, SectorColAmount, SectorMatrixColAmount);
            return (p1Sec1, p1Sec2, p2Sec1, p2Sec2);
        }
        void SubmitOverlappingSectors(int sourceSector, int targetSector)
        {
            int difference = targetSector - sourceSector;
            OverlappingDirection overlapping;
            switch (difference)
            {
                case 1:
                    overlapping = OverlappingDirection.E;
                    break;
                case > 1:
                    overlapping = OverlappingDirection.N;
                    break;
                case -1:
                    overlapping = OverlappingDirection.W;
                    break;
                default:
                    overlapping = OverlappingDirection.S;
                    break;
            }
            SectorOverlappingDirectionTable[sourceSector] |= overlapping;
        }
    }
    [Flags]
    enum OverlappingDirection : byte
    {
        None = 0,
        N = 1,
        E = 2,
        S = 4,
        W = 8,
    }
    [BurstCompile]
    internal struct ActiveWaveFront
    {
        internal int LocalIndex;
        internal float Distance;

        internal ActiveWaveFront(int localIndes, float distance)
        {
            LocalIndex = localIndes;
            Distance = distance;
        } 
    }
    internal struct NotActivePortalRecord
    {
        internal int CurSequenceIndex;
        internal int NextSequenceIndex;
        internal NotActivePortalRecord(int curActivePortalIndex, int nextActivePortalIndex)
        {
            CurSequenceIndex = curActivePortalIndex;
            NextSequenceIndex = nextActivePortalIndex;
        }
    }
}
