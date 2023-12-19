using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

public class RequestedSectorCalculationScheduler
{
    PathfindingManager _pathfindingManager;
    PathContainer _pathContainer;
    FlowCalculationScheduler _flowCalculationScheduler;
    NativeList<PathPipelineInfoWithHandle> ScheduledRequestedSectorCalculations;

    public RequestedSectorCalculationScheduler(PathfindingManager pathfindingManager, LOSIntegrationScheduler losScheduler)
    {
        _pathfindingManager = pathfindingManager;
        _pathContainer = pathfindingManager.PathContainer;
        ScheduledRequestedSectorCalculations = new NativeList<PathPipelineInfoWithHandle>(Allocator.Persistent);
        _flowCalculationScheduler = new FlowCalculationScheduler(pathfindingManager, losScheduler);
    }

    public void ScheduleRequestedSectorCalculation(PathPipelineInfoWithHandle pathInfo, JobHandle activePortalSubmissionHandle, NativeSlice<float2> sources)
    {
        Path path = _pathContainer.ProducedPaths[pathInfo.PathIndex];
        PathDestinationData destinationData = _pathContainer.PathDestinationDataList[pathInfo.PathIndex];
        PathLocationData locationData = _pathContainer.PathLocationDataList[pathInfo.PathIndex];
        PathPortalTraversalData portalTraversalData = _pathContainer.PathPortalTraversalDataList[pathInfo.PathIndex];
        UnsafeList<PathSectorState> sectorStateTable = _pathContainer.PathSectorStateTableList[pathInfo.PathIndex];
        FieldGraph pickedFieldGraph = _pathfindingManager.FieldProducer.GetFieldGraphWithOffset(destinationData.Offset);
        //SOURCE SECTOR CALCULATION
        SourceSectorCalculationJob sectorCalcJob = new SourceSectorCalculationJob()
        {
            SectorColAmount = FlowFieldUtilities.SectorColAmount,
            SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
            SectorMatrixRowAmount = FlowFieldUtilities.SectorMatrixRowAmount,
            LOSRange = FlowFieldUtilities.LOSRange,
            SectorSize = FlowFieldUtilities.SectorColAmount * FlowFieldUtilities.TileSize,
            SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
            TargetIndex = FlowFieldUtilities.PosTo2D(destinationData.Destination, FlowFieldUtilities.TileSize),
            SectorStateTable = sectorStateTable,
            SectorToPickedTable = locationData.SectorToPicked,
            Sources = sources,
            PortalSequence = portalTraversalData.PortalSequence,
            ActiveWaveFrontListArray = path.ActivePortalList,
            PortalNodes = pickedFieldGraph.PortalNodes,
            SectorFlowStartIndiciesToCalculateIntegration = path.SectorFlowStartIndiciesToCalculateIntegration,
            SectorFlowStartIndiciesToCalculateFlow = path.SectorFlowStartIndiciesToCalculateFlow,
            PickedToSectorTable = path.PickedToSector,
            SectorWithinLOSState = path.SectorWithinLOSState,
        };
        JobHandle sourceSectorHandle = sectorCalcJob.Schedule(activePortalSubmissionHandle);
        if (FlowFieldUtilities.DebugMode) { sourceSectorHandle.Complete(); }

        ScheduledRequestedSectorCalculations.Add(new PathPipelineInfoWithHandle(sourceSectorHandle, pathInfo.PathIndex));
    }
    public void TryComplete()
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

    public void ForceComplete()
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
