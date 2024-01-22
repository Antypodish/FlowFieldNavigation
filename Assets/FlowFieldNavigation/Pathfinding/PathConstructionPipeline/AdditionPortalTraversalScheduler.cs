using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

internal class AdditionPortalTraversalScheduler
{
    PathfindingManager _pathfindingManager;
    PathDataContainer _pathContainer;
    AdditionActivePortalSubmissionScheduler _additionActivePortalSubmissionScheduler;
    RequestedSectorCalculationScheduler _requestedSectorCalculationScheduler;
    NativeList<PathPipelineInfoWithHandle> ScheduledAdditionPortalTraversals;

    internal AdditionPortalTraversalScheduler(PathfindingManager pathfindingManager, RequestedSectorCalculationScheduler requestedSectorCalculationScheduler)
    {
        ScheduledAdditionPortalTraversals = new NativeList<PathPipelineInfoWithHandle>(Allocator.Persistent);
        _pathfindingManager = pathfindingManager;
        _pathContainer = _pathfindingManager.PathDataContainer;
        _additionActivePortalSubmissionScheduler = new AdditionActivePortalSubmissionScheduler(pathfindingManager);
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

        FieldGraph pickedFieldGraph = _pathfindingManager.FieldDataContainer.GetFieldGraphWithOffset(destinationData.Offset);

        PortalNodeAdditionReductionJob reductionJob = new PortalNodeAdditionReductionJob()
        {
            TargetIndex = FlowFieldUtilities.PosTo2D(destinationData.Destination, FlowFieldUtilities.TileSize),
            FieldTileSize = FlowFieldUtilities.TileSize,
            SectorColAmount = FlowFieldUtilities.SectorColAmount,
            SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
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
            AStarTraverseIndexList = portalTraversalData.AStartTraverseIndexList,
            IslandFields = pickedFieldGraph.IslandFields,
            SectorStateTable = sectorStateTable,
            DijkstraStartIndicies = new NativeList<int>(Allocator.Persistent),
        };

        PortalNodeAdditionTraversalJob travJob = new PortalNodeAdditionTraversalJob()
        {
            SectorColAmount = FlowFieldUtilities.SectorColAmount,
            SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
            SectorMatrixRowAmount = FlowFieldUtilities.SectorMatrixRowAmount,
            SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
            LOSRange = FlowFieldUtilities.LOSRange,
            Target = FlowFieldUtilities.PosTo2D(destinationData.Destination, FlowFieldUtilities.TileSize),
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
            IslandFields = pickedFieldGraph.IslandFields,
            SectorStateTable = sectorStateTable,
            DijkstraStartIndicies = reductionJob.DijkstraStartIndicies,
            AddedPortalSequenceBorderStartIndex = portalTraversalData.PortalSequenceBorders.Length,
            SectorWithinLOSState = internalData.SectorWithinLOSState,
            NewPickedSectorStartIndex = portalTraversalData.NewPickedSectorStartIndex,
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
        for(int i = ScheduledAdditionPortalTraversals.Length - 1; i >= 0; i--)
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