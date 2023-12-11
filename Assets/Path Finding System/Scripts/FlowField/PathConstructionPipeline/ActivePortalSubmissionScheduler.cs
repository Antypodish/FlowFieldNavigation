using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

internal class ActivePortalSubmissionScheduler
{
    PathfindingManager _pathfindingManager;
    PathContainer _pathProducer;

    public ActivePortalSubmissionScheduler(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
        _pathProducer = pathfindingManager.PathContainer;
    }

    public RequestPipelineInfoWithHandle ScheduleActivePortalSubmission(RequestPipelineInfoWithHandle reqInfo)
    {
        Path path = _pathProducer.ProducedPaths[reqInfo.PathIndex];
        PathLocationData locationData = _pathProducer.PathLocationDataList[reqInfo.PathIndex];
        FieldGraph pickedFieldGraph = _pathfindingManager.FieldProducer.GetFieldGraphWithOffset(path.Offset);

        //ACTIVE WAVE FRONT SUBMISSION
        ActivePortalSubmitJob submitJob = new ActivePortalSubmitJob()
        {
            SectorColAmount = FlowFieldUtilities.SectorColAmount,
            SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
            SectorMatrixRowAmount = FlowFieldUtilities.SectorMatrixRowAmount,
            SectorRowAmount = FlowFieldUtilities.SectorRowAmount,
            SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
            FieldColAmount = FlowFieldUtilities.FieldColAmount,
            TargetIndex2D = path.TargetIndex,

            PortalEdges = pickedFieldGraph.PorToPorPtrs,
            SectorToPicked = locationData.SectorToPicked,
            PickedToSector = path.PickedToSector,
            PortalSequence = path.PortalSequence,
            PortalSequenceBorders = path.PortalSequenceBorders,
            WinToSecPtrs = pickedFieldGraph.WinToSecPtrs,
            PortalNodes = pickedFieldGraph.PortalNodes,
            WindowNodes = pickedFieldGraph.WindowNodes,
            ActiveWaveFrontListArray = path.ActiveWaveFrontList,
            NotActivatedPortals = path.NotActivePortalList,
        };
        JobHandle submitHandle = submitJob.Schedule();

        if (FlowFieldUtilities.DebugMode) { submitHandle.Complete(); }

        reqInfo.Handle = submitHandle;
        return reqInfo;
    }
}
