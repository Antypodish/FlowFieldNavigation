﻿using Unity.Jobs;
using Unity.Burst;
using System.IO;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.XR;

namespace FlowFieldNavigation
{

    [BurstCompile]
    internal struct SourceSectorCalculationJob : IJob
    {
        internal float SectorSize;
        internal int FieldColAmount;
        internal int SectorColAmount;
        internal int SectorTileAmount;
        internal int SectorMatrixColAmount;
        internal int SectorMatrixRowAmount;
        internal int LOSRange;
        internal int2 TargetIndex;
        internal float2 FieldGridStartPos;
        [ReadOnly] internal NativeSlice<float2> Sources;
        [ReadOnly] internal UnsafeList<int> SectorToPickedTable;
        [ReadOnly] internal NativeArray<int> PickedToSectorTable;
        [ReadOnly] internal NativeArray<OverlappingDirection> SectorOverlappingDirectionTable;

        internal NativeList<int> SectorFlowStartIndiciesToCalculateIntegration;
        internal NativeList<int> SectorFlowStartIndiciesToCalculateFlow;

        internal UnsafeList<PathSectorState> SectorStateTable;
        internal NativeReference<SectorsWihinLOSArgument> SectorWithinLOSState;
        public void Execute()
        {
            SectorFlowStartIndiciesToCalculateFlow.Clear();
            SectorFlowStartIndiciesToCalculateIntegration.Clear();


            int targetSector1d = FlowFieldUtilities.GetSector1D(TargetIndex, SectorColAmount, SectorMatrixColAmount);
            for (int i = 0; i < Sources.Length; i++)
            {
                float2 pos = Sources[i];
                int sector1d = FlowFieldUtilities.PosToSector1D(pos, SectorSize, SectorMatrixColAmount, FieldGridStartPos);
                if ((SectorStateTable[sector1d] & PathSectorState.IntegrationCalculated) != PathSectorState.IntegrationCalculated)
                {
                    SectorStateTable[sector1d] |= PathSectorState.IntegrationCalculated;
                    int flowStartIndex = SectorToPickedTable[sector1d];
                    SectorFlowStartIndiciesToCalculateIntegration.Add(flowStartIndex);
                }
                if ((SectorStateTable[sector1d] & PathSectorState.FlowCalculated) != PathSectorState.FlowCalculated)
                {
                    SectorStateTable[sector1d] |= PathSectorState.FlowCalculated;
                    int flowStartIndex = SectorToPickedTable[sector1d];
                    SectorFlowStartIndiciesToCalculateFlow.Add(flowStartIndex);
                }

                //Handle overlapping sectors
                OverlappingDirection sectorOverlappingDirections = SectorOverlappingDirectionTable[sector1d];
                if((sectorOverlappingDirections & OverlappingDirection.N) == OverlappingDirection.N)
                {
                    int overlappingSector = sector1d + SectorMatrixColAmount;
                    PathSectorState overlappingSectorState = SectorStateTable[overlappingSector];
                    if((overlappingSectorState & PathSectorState.IntegrationCalculated) != PathSectorState.IntegrationCalculated)
                    {
                        SectorStateTable[overlappingSector] = overlappingSectorState | PathSectorState.IntegrationCalculated;
                        SectorFlowStartIndiciesToCalculateIntegration.Add(SectorToPickedTable[overlappingSector]);
                    }
                }
                if ((sectorOverlappingDirections & OverlappingDirection.E) == OverlappingDirection.E)
                {
                    int overlappingSector = sector1d + 1;
                    PathSectorState overlappingSectorState = SectorStateTable[overlappingSector];
                    if ((overlappingSectorState & PathSectorState.IntegrationCalculated) != PathSectorState.IntegrationCalculated)
                    {
                        SectorStateTable[overlappingSector] = overlappingSectorState | PathSectorState.IntegrationCalculated;
                        SectorFlowStartIndiciesToCalculateIntegration.Add(SectorToPickedTable[overlappingSector]);
                    }
                }
                if ((sectorOverlappingDirections & OverlappingDirection.S) == OverlappingDirection.S)
                {
                    int overlappingSector = sector1d - SectorMatrixColAmount;
                    PathSectorState overlappingSectorState = SectorStateTable[overlappingSector];
                    if ((overlappingSectorState & PathSectorState.IntegrationCalculated) != PathSectorState.IntegrationCalculated)
                    {
                        SectorStateTable[overlappingSector] = overlappingSectorState | PathSectorState.IntegrationCalculated;
                        SectorFlowStartIndiciesToCalculateIntegration.Add(SectorToPickedTable[overlappingSector]);
                    }
                }
                if ((sectorOverlappingDirections & OverlappingDirection.W) == OverlappingDirection.W)
                {
                    int overlappingSector = sector1d - 1;
                    PathSectorState overlappingSectorState = SectorStateTable[overlappingSector];
                    if ((overlappingSectorState & PathSectorState.IntegrationCalculated) != PathSectorState.IntegrationCalculated)
                    {
                        SectorStateTable[overlappingSector] = overlappingSectorState | PathSectorState.IntegrationCalculated;
                        SectorFlowStartIndiciesToCalculateIntegration.Add(SectorToPickedTable[overlappingSector]);
                    }
                }
            }

            if (ContainsSectorsWithinLOSRange(SectorFlowStartIndiciesToCalculateIntegration.AsArray()))
            {
                SectorsWihinLOSArgument argument = SectorWithinLOSState.Value;
                argument |= SectorsWihinLOSArgument.RequestedSectorWithinLOS;
                SectorWithinLOSState.Value = argument;
            }
        }
        bool ContainsSectorsWithinLOSRange(NativeArray<int> integrationRequestedSectors)
        {
            int losRange = LOSRange;
            int sectorColAmount = SectorColAmount;
            int sectorMatrixColAmount = SectorMatrixColAmount;
            int sectorMatrixRowAmount = SectorMatrixRowAmount;
            int sectorTileAmount = SectorTileAmount;

            int2 targetSector2d = FlowFieldUtilities.GetSector2D(TargetIndex, sectorColAmount);
            int extensionLength = losRange / sectorColAmount + math.select(0, 1, losRange % sectorColAmount > 0);
            int2 rangeTopRightSector = targetSector2d + new int2(extensionLength, extensionLength);
            int2 rangeBotLeftSector = targetSector2d - new int2(extensionLength, extensionLength);
            rangeTopRightSector = new int2()
            {
                x = math.select(rangeTopRightSector.x, sectorMatrixColAmount - 1, rangeTopRightSector.x >= sectorMatrixColAmount),
                y = math.select(rangeTopRightSector.y, sectorMatrixRowAmount - 1, rangeTopRightSector.y >= sectorMatrixRowAmount)
            };
            rangeBotLeftSector = new int2()
            {
                x = math.select(rangeBotLeftSector.x, 0, rangeBotLeftSector.x < 0),
                y = math.select(rangeBotLeftSector.y, 0, rangeBotLeftSector.y < 0)
            };
            for (int i = 0; i < integrationRequestedSectors.Length; i++)
            {
                int sectorFlowStart = integrationRequestedSectors[i];
                int sector1d = PickedToSectorTable[(sectorFlowStart - 1) / sectorTileAmount];
                int sectorCol = sector1d % sectorMatrixColAmount;
                int sectorRow = sector1d / sectorMatrixColAmount;

                bool withinColRange = sectorCol >= rangeBotLeftSector.x && sectorCol <= rangeTopRightSector.x;
                bool withinRowRange = sectorRow >= rangeBotLeftSector.y && sectorRow <= rangeTopRightSector.y;
                if (withinColRange && withinRowRange) { return true; }
            }
            return false;
        }
    }

}