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
        AdditionActivePortalSubmissionScheduler _additionActivePortalSubmissionScheduler;
        RequestedSectorCalculationScheduler _requestedSectorCalculationScheduler;
        NativeList<PathPipelineInfoWithHandle> ScheduledAdditionPortalTraversals;

        internal AdditionPortalTraversalScheduler(FlowFieldNavigationManager navigationManager, RequestedSectorCalculationScheduler requestedSectorCalculationScheduler)
        {
            ScheduledAdditionPortalTraversals = new NativeList<PathPipelineInfoWithHandle>(Allocator.Persistent);
            _navigationManager = navigationManager;
            _pathContainer = _navigationManager.PathDataContainer;
            _additionActivePortalSubmissionScheduler = new AdditionActivePortalSubmissionScheduler(navigationManager);
            _requestedSectorCalculationScheduler = requestedSectorCalculationScheduler;
        }
        internal void DisposeAll()
        {
            if (ScheduledAdditionPortalTraversals.IsCreated) { ScheduledAdditionPortalTraversals.Dispose(); }
            _requestedSectorCalculationScheduler.DisposeAll();
            _additionActivePortalSubmissionScheduler = null;
            _requestedSectorCalculationScheduler = null;
        }
        internal void SchedulePortalTraversalFor(PathPipelineInfoWithHandle pathInfo, NativeSlice<float2> sources)
        {
            PathfindingInternalData internalData = _pathContainer.PathfindingInternalDataList[pathInfo.PathIndex];
            PathDestinationData destinationData = _pathContainer.PathDestinationDataList[pathInfo.PathIndex];
            UnsafeList<PathSectorState> sectorStateTable = _pathContainer.PathSectorStateTableList[pathInfo.PathIndex];
            PathPortalTraversalData portalTraversalData = _pathContainer.PathPortalTraversalDataList[pathInfo.PathIndex];
            PathLocationData locationData = _pathContainer.PathLocationDataList[pathInfo.PathIndex];
            portalTraversalData.PathAdditionSequenceBorderStartIndex.Value = portalTraversalData.PortalSequenceBorders.Length - 1;
            portalTraversalData.NewPickedSectorStartIndex.Value = internalData.PickedSectorList.Length;

            FieldGraph pickedFieldGraph = _navigationManager.FieldDataContainer.GetFieldGraphWithOffset(destinationData.Offset);
            CostField costField = _navigationManager.FieldDataContainer.GetCostFieldWithOffset(destinationData.Offset);
            
            PortalReductionJob reductionJob = new PortalReductionJob()
            {
                FieldColAmount = FlowFieldUtilities.FieldColAmount,
                FieldRowAmount = FlowFieldUtilities.FieldRowAmount,
                SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
                TargetIndex = FlowFieldUtilities.PosTo2D(destinationData.Destination, FlowFieldUtilities.TileSize, FlowFieldUtilities.FieldGridStartPosition),
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
                PortalTraversalDataArray = portalTraversalData.PortalTraversalDataArray,
                SourcePortalIndexList = portalTraversalData.SourcePortalIndexList,
                IslandFields = pickedFieldGraph.IslandFields,
                SectorStateTable = sectorStateTable,
                DijkstraStartIndicies = portalTraversalData.DiskstraStartIndicies,
                SectorToPicked = locationData.SectorToPicked,
                Costs = costField.Costs,
                LocalDirections = _navigationManager.FieldDataContainer.GetSectorDirections(),
                FlowFieldLength = internalData.FlowFieldLength,
                TargetNeighbourPortalIndicies = portalTraversalData.TargetSectorPortalIndexList,
            };

            PortalTraversalJob travJob = new PortalTraversalJob()
            {
                FieldColAmount  = FlowFieldUtilities.FieldColAmount,
                SectorColAmount = FlowFieldUtilities.SectorColAmount,
                SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
                SectorMatrixRowAmount = FlowFieldUtilities.SectorMatrixRowAmount,
                SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
                LOSRange = FlowFieldUtilities.LOSRange,
                Target = FlowFieldUtilities.PosTo2D(destinationData.Destination, FlowFieldUtilities.TileSize, FlowFieldUtilities.FieldGridStartPosition),
                PickedToSector = internalData.PickedSectorList,
                PortalSequenceBorders = portalTraversalData.PortalSequenceBorders,
                PortalNodes = pickedFieldGraph.PortalNodes,
                WindowNodes = pickedFieldGraph.WindowNodes,
                WinToSecPtrs = pickedFieldGraph.WinToSecPtrs,
                PorPtrs = pickedFieldGraph.PorToPorPtrs,
                SectorNodes = pickedFieldGraph.SectorNodes,
                PortalSequence = portalTraversalData.PortalSequence,
                FlowFieldLength = internalData.FlowFieldLength,
                PortalTraversalDataArray = portalTraversalData.PortalTraversalDataArray,
                SourcePortalIndexList = portalTraversalData.SourcePortalIndexList,
                SectorStateTable = sectorStateTable,
                DijkstraStartIndicies = portalTraversalData.DiskstraStartIndicies,
                AddedPortalSequenceBorderStartIndex = portalTraversalData.PortalSequenceBorders.Length,
                SectorWithinLOSState = internalData.SectorWithinLOSState,
                NewPickedSectorStartIndex = portalTraversalData.NewPickedSectorStartIndex,
                SectorToPicked = locationData.SectorToPicked,
                TargetNeighbourPortalIndicies = portalTraversalData.TargetSectorPortalIndexList,
            };
            JobHandle reductHandle = reductionJob.Schedule();
            JobHandle travHandle = travJob.Schedule(reductHandle);

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
                    PathPipelineInfoWithHandle _addActivePorSubmitHandle = _additionActivePortalSubmissionScheduler.ScheduleActivePortalSubmission(pathInfo);
                    PathRoutineData existingPath = pathRoutineDataList[pathInfo.PathIndex];
                    int flowStart = math.select(existingPath.PathAdditionSourceStart, existingPath.FlowRequestSourceStart, existingPath.FlowRequestSourceCount != 0);
                    int flowCount = existingPath.FlowRequestSourceCount + existingPath.PathAdditionSourceCount;
                    NativeSlice<float2> sourcePositions = new NativeSlice<float2>(sources, flowStart, flowCount);
                    _requestedSectorCalculationScheduler.ScheduleRequestedSectorCalculation(pathInfo, _addActivePorSubmitHandle.Handle, sourcePositions);

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
                PathPipelineInfoWithHandle _addActivePorSubmitHandle = _additionActivePortalSubmissionScheduler.ScheduleActivePortalSubmission(pathInfo);
                PathRoutineData existingPath = pathRoutineDataList[pathInfo.PathIndex];
                int flowStart = math.select(existingPath.PathAdditionSourceStart, existingPath.FlowRequestSourceStart, existingPath.FlowRequestSourceCount != 0);
                int flowCount = existingPath.FlowRequestSourceCount + existingPath.PathAdditionSourceCount;
                NativeSlice<float2> sourcePositions = new NativeSlice<float2>(sources, flowStart, flowCount);
                _requestedSectorCalculationScheduler.ScheduleRequestedSectorCalculation(pathInfo, _addActivePorSubmitHandle.Handle, sourcePositions);
            }
            ScheduledAdditionPortalTraversals.Clear();
        }
    }

}