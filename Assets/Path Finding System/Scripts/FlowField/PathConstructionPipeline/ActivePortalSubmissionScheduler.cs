using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

internal class ActivePortalSubmissionScheduler
{
    PathfindingManager _pathfindingManager;
    PathContainer _pathContainer;

    public ActivePortalSubmissionScheduler(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
        _pathContainer = pathfindingManager.PathContainer;
    }

    public RequestPipelineInfoWithHandle ScheduleActivePortalSubmission(RequestPipelineInfoWithHandle reqInfo)
    {
        Path path = _pathContainer.ProducedPaths[reqInfo.PathIndex];
        PathDestinationData destinationData = _pathContainer.PathDestinationDataList[reqInfo.PathIndex];
        PathLocationData locationData = _pathContainer.PathLocationDataList[reqInfo.PathIndex];
        PathPortalTraversalData portalTraversalData = _pathContainer.PathPortalTraversalDataList[reqInfo.PathIndex];
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
            TargetIndex2D = destinationData.TargetIndex,

            PortalEdges = pickedFieldGraph.PorToPorPtrs,
            SectorToPicked = locationData.SectorToPicked,
            PickedToSector = path.PickedToSector,
            PortalSequence = portalTraversalData.PortalSequence,
            PortalSequenceBorders = portalTraversalData.PortalSequenceBorders,
            WinToSecPtrs = pickedFieldGraph.WinToSecPtrs,
            PortalNodes = pickedFieldGraph.PortalNodes,
            WindowNodes = pickedFieldGraph.WindowNodes,
            ActiveWaveFrontListArray = path.ActivePortalList,
            NotActivatedPortals = path.NotActivePortalList,
        };
        JobHandle submitHandle = submitJob.Schedule();

        if (FlowFieldUtilities.DebugMode) { submitHandle.Complete(); }

        reqInfo.Handle = submitHandle;
        return reqInfo;
    }
}
