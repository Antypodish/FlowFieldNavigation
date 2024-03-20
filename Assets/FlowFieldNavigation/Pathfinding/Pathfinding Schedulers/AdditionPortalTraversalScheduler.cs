using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using System.Diagnostics;


namespace FlowFieldNavigation
{
    internal class AdditionPortalTraversalScheduler
    {
        FlowFieldNavigationManager _navigationManager;
        PathDataContainer _pathContainer;
        ActivePortalSubmissionScheduler _activePortalSubmissionScheduler;
        RequestedSectorCalculationScheduler _requestedSectorCalculationScheduler;
        PortalTraversalDataProvider _porTravDataProvider;
        NativeList<PathPipelineInfoWithHandle> ScheduledAdditionPortalTraversals;

        internal AdditionPortalTraversalScheduler(FlowFieldNavigationManager navManager, RequestedSectorCalculationScheduler reqSecCalcScheduler, PortalTraversalDataProvider porTravDataProvider)
        {
            ScheduledAdditionPortalTraversals = new NativeList<PathPipelineInfoWithHandle>(Allocator.Persistent);
            _navigationManager = navManager;
            _pathContainer = _navigationManager.PathDataContainer;
            _activePortalSubmissionScheduler = new ActivePortalSubmissionScheduler(navManager);
            _requestedSectorCalculationScheduler = reqSecCalcScheduler;
            _porTravDataProvider = porTravDataProvider;
        }
        internal void DisposeAll()
        {
            if (ScheduledAdditionPortalTraversals.IsCreated) { ScheduledAdditionPortalTraversals.Dispose(); }
            _requestedSectorCalculationScheduler.DisposeAll();
            _activePortalSubmissionScheduler = null;
            _requestedSectorCalculationScheduler = null;
        }
        internal void SchedulePortalTraversalFor(PathPipelineInfoWithHandle pathInfo, NativeSlice<float2> sources)
        {
            PathfindingInternalData internalData = _pathContainer.PathfindingInternalDataList[pathInfo.PathIndex];
            PathDestinationData destinationData = _pathContainer.PathDestinationDataList[pathInfo.PathIndex];
            PathPortalTraversalData portalTraversalData = _pathContainer.PathPortalTraversalDataList[pathInfo.PathIndex];
            UnsafeList<PathSectorState> sectorStateTable = _pathContainer.PathSectorStateTableList[pathInfo.PathIndex];
            int2 destinationIndex = FlowFieldUtilities.PosTo2D(destinationData.Destination, FlowFieldUtilities.TileSize, FlowFieldUtilities.FieldGridStartPosition);
            CostField pickedCostField = _navigationManager.FieldDataContainer.GetCostFieldWithOffset(destinationData.Offset);
            FieldGraph pickedFieldGraph = _navigationManager.FieldDataContainer.GetFieldGraphWithOffset(destinationData.Offset);
            portalTraversalData.PathAdditionSequenceSliceStartIndex.Value = portalTraversalData.PortalSequenceSlices.Length;
            portalTraversalData.NewPickedSectorStartIndex.Value = internalData.PickedSectorList.Length;


            NativeArray<PortalTraversalData> porTravDataArray = _porTravDataProvider.GetAvailableData(out JobHandle dependency);
            PortalReductionJob reductionJob = new PortalReductionJob()
            {
                TileSize = FlowFieldUtilities.TileSize,
                FieldColAmount = FlowFieldUtilities.FieldColAmount,
                FieldRowAmount = FlowFieldUtilities.FieldRowAmount,
                SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
                TargetIndex = destinationIndex,
                FieldTileSize = FlowFieldUtilities.TileSize,
                SectorColAmount = FlowFieldUtilities.SectorColAmount,
                SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
                FieldGridStartPos = FlowFieldUtilities.FieldGridStartPosition,
                PickedToSector = internalData.PickedSectorList,
                TargetSectorCosts = _pathContainer.TargetSectorIntegrationList[pathInfo.PathIndex],
                PortalNodes = pickedFieldGraph.PortalNodes,
                SecToWinPtrs = pickedFieldGraph.SecToWinPtrs,
                WindowNodes = pickedFieldGraph.WindowNodes,
                WinToSecPtrs = pickedFieldGraph.WinToSecPtrs,
                SourcePositions = sources,
                PorPtrs = pickedFieldGraph.PorToPorPtrs,
                SectorNodes = pickedFieldGraph.SectorNodes,
                PortalTraversalDataArray = porTravDataArray,
                SourcePortalIndexList = portalTraversalData.SourcePortalIndexList,
                IslandFields = pickedFieldGraph.IslandFields,
                SectorStateTable = sectorStateTable,
                DijkstraStartIndicies = portalTraversalData.DiskstraStartIndicies,
                Costs = pickedCostField.Costs,
                LocalDirections = _navigationManager.FieldDataContainer.GetSectorDirections(),
                GoalTraversalDataList = portalTraversalData.GoalDataList,
                NewReducedNodeIndicies = portalTraversalData.NewReducedPortalIndicies,
                PortalDataRecords = portalTraversalData.PortalDataRecords.AsArray(),
            };

            PortalTraversalJob travJob = new PortalTraversalJob()
            {
                FieldColAmount  = FlowFieldUtilities.FieldColAmount,
                SectorColAmount = FlowFieldUtilities.SectorColAmount,
                SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
                SectorMatrixRowAmount = FlowFieldUtilities.SectorMatrixRowAmount,
                SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
                LOSRange = FlowFieldUtilities.LOSRange,
                Target = destinationIndex,
                PickedSectorIndicies = internalData.PickedSectorList,
                PortalSequenceSlices = portalTraversalData.PortalSequenceSlices,
                PortalNodes = pickedFieldGraph.PortalNodes,
                WindowNodes = pickedFieldGraph.WindowNodes,
                WinToSecPtrs = pickedFieldGraph.WinToSecPtrs,
                PorPtrs = pickedFieldGraph.PorToPorPtrs,
                SectorNodes = pickedFieldGraph.SectorNodes,
                PortalSequence = portalTraversalData.PortalSequence,
                FlowFieldLength = internalData.FlowFieldLength,
                PortalTraversalDataArray = porTravDataArray,
                SourcePortalIndexList = portalTraversalData.SourcePortalIndexList,
                SectorStateTable = sectorStateTable,
                DijkstraStartIndicies = portalTraversalData.DiskstraStartIndicies,
                NewPortalSliceStartIndex = portalTraversalData.PathAdditionSequenceSliceStartIndex.Value,
                SectorWithinLOSState = internalData.SectorWithinLOSState,
                NewPickedSectorStartIndex = portalTraversalData.NewPickedSectorStartIndex,
                NewReducedPortalIndicies = portalTraversalData.NewReducedPortalIndicies,
                PortalDataRecords = portalTraversalData.PortalDataRecords,
            };
            JobHandle reductHandle = reductionJob.Schedule(dependency);
            JobHandle travHandle = travJob.Schedule(reductHandle);
            _porTravDataProvider.IncerimentPointer(travHandle);
            if (FlowFieldUtilities.DebugMode) { travHandle.Complete(); }
            pathInfo.Handle = travHandle;
            ScheduledAdditionPortalTraversals.Add(pathInfo);
        }

        internal void TryComplete(NativeArray<float2> sources)
        {
            NativeList<PathRoutineData> pathRoutineDataList = _pathContainer.PathRoutineDataList;
            for (int i = ScheduledAdditionPortalTraversals.Length - 1; i >= 0; i--)
            {
                PathPipelineInfoWithHandle pathInfo = ScheduledAdditionPortalTraversals[i];
                if (pathInfo.Handle.IsCompleted)
                {
                    pathInfo.Handle.Complete();
                    ScheduledAdditionPortalTraversals.RemoveAtSwapBack(i);

                    //SCHEDULE ADDITION ACTIVE PORTAL SUBMIT JOB
                    pathInfo.Handle = _activePortalSubmissionScheduler.ScheduleActivePortalSubmission(pathInfo.PathIndex, pathInfo.Handle);
                    PathRoutineData existingPath = pathRoutineDataList[pathInfo.PathIndex];
                    int flowStart = math.select(existingPath.PathAdditionSourceStart, existingPath.FlowRequestSourceStart, existingPath.FlowRequestSourceCount != 0);
                    int flowCount = existingPath.FlowRequestSourceCount + existingPath.PathAdditionSourceCount;
                    NativeSlice<float2> sourcePositions = new NativeSlice<float2>(sources, flowStart, flowCount);
                    _requestedSectorCalculationScheduler.ScheduleRequestedSectorCalculation(pathInfo.PathIndex, pathInfo.Handle, pathInfo.DestinationState, sourcePositions);

                }
            }
        }
        internal void ForceComplete(NativeArray<float2> sources)
        {
            NativeList<PathRoutineData> pathRoutineDataList = _pathContainer.PathRoutineDataList;
            for (int i = ScheduledAdditionPortalTraversals.Length - 1; i >= 0; i--)
            {
                PathPipelineInfoWithHandle pathInfo = ScheduledAdditionPortalTraversals[i];
                pathInfo.Handle.Complete();
                ScheduledAdditionPortalTraversals.RemoveAtSwapBack(i);

                //SCHEDULE ADDITION ACTIVE PORTAL SUBMIT JOB
                pathInfo.Handle = _activePortalSubmissionScheduler.ScheduleActivePortalSubmission(pathInfo.PathIndex, pathInfo.Handle);
                PathRoutineData existingPath = pathRoutineDataList[pathInfo.PathIndex];
                int flowStart = math.select(existingPath.PathAdditionSourceStart, existingPath.FlowRequestSourceStart, existingPath.FlowRequestSourceCount != 0);
                int flowCount = existingPath.FlowRequestSourceCount + existingPath.PathAdditionSourceCount;
                NativeSlice<float2> sourcePositions = new NativeSlice<float2>(sources, flowStart, flowCount);
                _requestedSectorCalculationScheduler.ScheduleRequestedSectorCalculation(pathInfo.PathIndex, pathInfo.Handle, pathInfo.DestinationState, sourcePositions);
            }
            ScheduledAdditionPortalTraversals.Clear();
        }
    }

}