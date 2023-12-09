using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

public class RequestedSectorCalculationScheduler
{
    PathfindingManager _pathfindingManager;
    PathContainer _pathProducer;
    FlowCalculationScheduler _flowCalculationScheduler;
    NativeList<PathPipelineInfoWithHandle> ScheduledRequestedSectorCalculations;

    public RequestedSectorCalculationScheduler(PathfindingManager pathfindingManager, LOSIntegrationScheduler losScheduler)
    {
        _pathfindingManager = pathfindingManager;
        _pathProducer = pathfindingManager.PathContainer;
        ScheduledRequestedSectorCalculations = new NativeList<PathPipelineInfoWithHandle>(Allocator.Persistent);
        _flowCalculationScheduler = new FlowCalculationScheduler(pathfindingManager, losScheduler);
    }

    public void ScheduleRequestedSectorCalculation(PathPipelineInfoWithHandle pathInfo, JobHandle activePortalSubmissionHandle, NativeSlice<float2> sources)
    {
        Path path = _pathProducer.ProducedPaths[pathInfo.PathIndex];
        FieldGraph pickedFieldGraph = _pathfindingManager.FieldProducer.GetFieldGraphWithOffset(path.Offset);
        //SOURCE SECTOR CALCULATION
        SourceSectorCalculationJob sectorCalcJob = new SourceSectorCalculationJob()
        {
            SectorColAmount = FlowFieldUtilities.SectorColAmount,
            SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
            SectorMatrixRowAmount = FlowFieldUtilities.SectorMatrixRowAmount,
            LOSRange = FlowFieldUtilities.LOSRange,
            SectorSize = FlowFieldUtilities.SectorColAmount * FlowFieldUtilities.TileSize,
            SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
            TargetIndex = path.TargetIndex,
            SectorStateTable = path.SectorStateTable,
            SectorToPickedTable = path.SectorToPicked,
            Sources = sources,
            PortalSequence = path.PortalSequence,
            ActiveWaveFrontListArray = path.ActiveWaveFrontList,
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
