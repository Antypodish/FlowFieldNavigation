using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

internal class RequestedSectorCalculationScheduler
{
    PathfindingManager _pathfindingManager;
    PathDataContainer _pathContainer;
    FlowCalculationScheduler _flowCalculationScheduler;
    NativeList<PathPipelineInfoWithHandle> ScheduledRequestedSectorCalculations;

    internal RequestedSectorCalculationScheduler(PathfindingManager pathfindingManager, LOSIntegrationScheduler losScheduler)
    {
        _pathfindingManager = pathfindingManager;
        _pathContainer = pathfindingManager.PathDataContainer;
        ScheduledRequestedSectorCalculations = new NativeList<PathPipelineInfoWithHandle>(Allocator.Persistent);
        _flowCalculationScheduler = new FlowCalculationScheduler(pathfindingManager, losScheduler);
    }
    internal void DisposeAll()
    {
        if (ScheduledRequestedSectorCalculations.IsCreated) { ScheduledRequestedSectorCalculations.Dispose(); }
        if(_flowCalculationScheduler != null) { _flowCalculationScheduler.DisposeAll(); }       
        _flowCalculationScheduler = null;
    }
    internal void ScheduleRequestedSectorCalculation(PathPipelineInfoWithHandle pathInfo, JobHandle activePortalSubmissionHandle, NativeSlice<float2> sources)
    {
        PathfindingInternalData pathInternalData = _pathContainer.PathfindingInternalDataList[pathInfo.PathIndex];
        PathDestinationData destinationData = _pathContainer.PathDestinationDataList[pathInfo.PathIndex];
        PathLocationData locationData = _pathContainer.PathLocationDataList[pathInfo.PathIndex];
        PathPortalTraversalData portalTraversalData = _pathContainer.PathPortalTraversalDataList[pathInfo.PathIndex];
        UnsafeList<PathSectorState> sectorStateTable = _pathContainer.PathSectorStateTableList[pathInfo.PathIndex];
        FieldGraph pickedFieldGraph = _pathfindingManager.FieldDataContainer.GetFieldGraphWithOffset(destinationData.Offset);
        //SOURCE SECTOR CALCULATION
        SourceSectorCalculationJob sectorCalcJob = new SourceSectorCalculationJob()
        {
            SectorColAmount = FlowFieldUtilities.SectorColAmount,
            SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
            SectorMatrixRowAmount = FlowFieldUtilities.SectorMatrixRowAmount,
            LOSRange = FlowFieldUtilities.LOSRange,
            SectorSize = FlowFieldUtilities.SectorColAmount * FlowFieldUtilities.TileSize,
            SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
            TargetIndex = FlowFieldUtilities.PosTo2D(destinationData.Destination, FlowFieldUtilities.TileSize, FlowFieldUtilities.FieldGridStartPosition),
            FieldGridStartPos = FlowFieldUtilities.FieldGridStartPosition,
            SectorStateTable = sectorStateTable,
            SectorToPickedTable = locationData.SectorToPicked,
            Sources = sources,
            PortalSequence = portalTraversalData.PortalSequence,
            ActiveWaveFrontListArray = pathInternalData.ActivePortalList,
            PortalNodes = pickedFieldGraph.PortalNodes,
            SectorFlowStartIndiciesToCalculateIntegration = pathInternalData.SectorFlowStartIndiciesToCalculateIntegration,
            SectorFlowStartIndiciesToCalculateFlow = pathInternalData.SectorFlowStartIndiciesToCalculateFlow,
            PickedToSectorTable = pathInternalData.PickedSectorList,
            SectorWithinLOSState = pathInternalData.SectorWithinLOSState,
        };
        JobHandle sourceSectorHandle = sectorCalcJob.Schedule(activePortalSubmissionHandle);
        if (FlowFieldUtilities.DebugMode)
        {
            sourceSectorHandle.Complete();
        }
        ScheduledRequestedSectorCalculations.Add(new PathPipelineInfoWithHandle(sourceSectorHandle, pathInfo.PathIndex));
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
