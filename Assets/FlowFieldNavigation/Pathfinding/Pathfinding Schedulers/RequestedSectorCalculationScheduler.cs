﻿using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace FlowFieldNavigation
{

    internal class RequestedSectorCalculationScheduler
    {
        FlowFieldNavigationManager _navigationManager;
        PathDataContainer _pathContainer;
        FlowCalculationScheduler _flowCalculationScheduler;
        NativeList<PathPipelineInfoWithHandle> ScheduledRequestedSectorCalculations;

        internal RequestedSectorCalculationScheduler(FlowFieldNavigationManager navigationManager, LOSIntegrationScheduler losScheduler)
        {
            _navigationManager = navigationManager;
            _pathContainer = navigationManager.PathDataContainer;
            ScheduledRequestedSectorCalculations = new NativeList<PathPipelineInfoWithHandle>(Allocator.Persistent);
            _flowCalculationScheduler = new FlowCalculationScheduler(navigationManager, losScheduler);
        }
        internal void DisposeAll()
        {
            if (ScheduledRequestedSectorCalculations.IsCreated) { ScheduledRequestedSectorCalculations.Dispose(); }
            if (_flowCalculationScheduler != null) { _flowCalculationScheduler.DisposeAll(); }
            _flowCalculationScheduler = null;
        }
        internal void ScheduleRequestedSectorCalculation(int pathIndex, JobHandle activePortalSubmissionHandle, DynamicDestinationState destinationState, NativeSlice<float2> sources)
        {
            PathfindingInternalData pathInternalData = _pathContainer.PathfindingInternalDataList[pathIndex];
            PathDestinationData destinationData = _pathContainer.PathDestinationDataList[pathIndex];
            UnsafeList<PathSectorState> sectorStateTable = _pathContainer.PathSectorStateTableList[pathIndex];
            NativeArray<OverlappingDirection> sectorOverlappingDirectionTable = _pathContainer.SectorOverlappingDirectionTableList[pathIndex];

            //SOURCE SECTOR CALCULATION
            SourceSectorCalculationJob sectorCalcJob = new SourceSectorCalculationJob()
            {
                FieldColAmount = FlowFieldUtilities.FieldColAmount,
                SectorColAmount = FlowFieldUtilities.SectorColAmount,
                SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
                SectorMatrixRowAmount = FlowFieldUtilities.SectorMatrixRowAmount,
                LOSRange = FlowFieldUtilities.LOSRange,
                SectorSize = FlowFieldUtilities.SectorColAmount * FlowFieldUtilities.TileSize,
                SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
                TargetIndex = FlowFieldUtilities.PosTo2D(destinationData.Destination, FlowFieldUtilities.TileSize, FlowFieldUtilities.FieldGridStartPosition),
                FieldGridStartPos = FlowFieldUtilities.FieldGridStartPosition,
                SectorStateTable = sectorStateTable,
                Sources = sources,
                SectorOverlappingDirectionTable = sectorOverlappingDirectionTable,
                SectorIndiciesToCalculateIntegration = pathInternalData.SectorIndiciesToCalculateIntegration,
                SectorIndiciesToCalculateFlow = pathInternalData.SectorIndiciesToCalculateFlow,
                SectorWithinLOSState = pathInternalData.SectorWithinLOSState,
            };
            JobHandle sourceSectorHandle = sectorCalcJob.Schedule(activePortalSubmissionHandle);
            if (FlowFieldUtilities.DebugMode)
            {
                sourceSectorHandle.Complete();
            }
            ScheduledRequestedSectorCalculations.Add(new PathPipelineInfoWithHandle(sourceSectorHandle, pathIndex, destinationState));
        }
        internal void TryComplete()
        {
            for (int i = ScheduledRequestedSectorCalculations.Length - 1; i >= 0; i--)
            {
                PathPipelineInfoWithHandle pathInfo = ScheduledRequestedSectorCalculations[i];
                pathInfo.Handle.Complete();
                _flowCalculationScheduler.ScheduleFlow(pathInfo);
            }
            ScheduledRequestedSectorCalculations.Clear();
            _flowCalculationScheduler.TryComplete();
        }

        internal void ForceComplete()
        {
            for (int i = ScheduledRequestedSectorCalculations.Length - 1; i >= 0; i--)
            {
                PathPipelineInfoWithHandle pathInfo = ScheduledRequestedSectorCalculations[i];
                pathInfo.Handle.Complete();
                _flowCalculationScheduler.ScheduleFlow(pathInfo);
            }
            ScheduledRequestedSectorCalculations.Clear();
            _flowCalculationScheduler.ForceComplete();
        }
    }


}