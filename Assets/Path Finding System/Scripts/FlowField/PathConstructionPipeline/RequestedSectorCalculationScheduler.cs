using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

public class RequestedSectorCalculationScheduler
{
    PathfindingManager _pathfindingManager;
    PathContainer _pathProducer;
    FlowCalculationScheduler _flowCalculationScheduler;
    NativeList<HandleWithPathIndex> ScheduledRequestedSectorCalculations;

    public RequestedSectorCalculationScheduler(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
        _pathProducer = pathfindingManager.PathProducer;
        ScheduledRequestedSectorCalculations = new NativeList<HandleWithPathIndex>(Allocator.Persistent);
        _flowCalculationScheduler = new FlowCalculationScheduler(pathfindingManager);
    }

    public void ScheduleRequestedSectorCalculation(int pathIndex, JobHandle activePortalSubmissionHandle, NativeSlice<float2> sources)
    {
        Path path = _pathProducer.ProducedPaths[pathIndex];
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
        ScheduledRequestedSectorCalculations.Add(new HandleWithPathIndex(sourceSectorHandle, pathIndex));
    }

    public void TryComplete()
    {
        for (int i = ScheduledRequestedSectorCalculations.Length - 1; i >= 0; i--)
        {
            HandleWithPathIndex sectorReqHandle = ScheduledRequestedSectorCalculations[i];
            if (sectorReqHandle.Handle.IsCompleted)
            {
                sectorReqHandle.Handle.Complete();
                _flowCalculationScheduler.ScheduleFlow(sectorReqHandle.PathIndex);
                ScheduledRequestedSectorCalculations.RemoveAtSwapBack(i);
            }
        }
        _flowCalculationScheduler.TryComplete();
    }

    public void ForceComplete()
    {
        for (int i = ScheduledRequestedSectorCalculations.Length - 1; i >= 0; i--)
        {
            HandleWithPathIndex sectorReqHandle = ScheduledRequestedSectorCalculations[i];
            sectorReqHandle.Handle.Complete();
            _flowCalculationScheduler.ScheduleFlow(sectorReqHandle.PathIndex);
        }
        ScheduledRequestedSectorCalculations.Clear();
        _flowCalculationScheduler.ForceComplete();
    }
}
