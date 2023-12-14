using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

internal class AdditionActivePortalSubmissionScheduler
{
    PathfindingManager _pathfindingManager;
    PathContainer _pathContainer;

    public AdditionActivePortalSubmissionScheduler(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
        _pathContainer = pathfindingManager.PathContainer;
    }

    public PathPipelineInfoWithHandle ScheduleActivePortalSubmission(PathPipelineInfoWithHandle pathInfo)
    {
        Path path = _pathContainer.ProducedPaths[pathInfo.PathIndex];
        PathLocationData locationData = _pathContainer.PathLocationDataList[pathInfo.PathIndex];
        UnsafeList<PathSectorState> sectorStateTable = _pathContainer.PathSectorStateTableList[pathInfo.PathIndex];
        FieldGraph pickedFieldGraph = _pathfindingManager.FieldProducer.GetFieldGraphWithOffset(path.Offset);
        int newSecorCount = path.PickedToSector.Length - path.ActivePortalList.Length;
        _pathContainer.ResizeActiveWaveFrontList(newSecorCount, path.ActivePortalList);

        //ACTIVE WAVE FRONT SUBMISSION
        AdditionalActivePortalSubmitJob submitJob = new AdditionalActivePortalSubmitJob()
        {
            SectorColAmount = FlowFieldUtilities.SectorColAmount,
            SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
            SectorMatrixRowAmount = FlowFieldUtilities.SectorMatrixRowAmount,
            SectorRowAmount = FlowFieldUtilities.SectorRowAmount,
            SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
            FieldColAmount = FlowFieldUtilities.FieldColAmount,
            TargetIndex2D = path.TargetIndex,
            SequenceBorderListStartIndex = path.PathAdditionSequenceBorderStartIndex[0],

            PortalEdges = pickedFieldGraph.PorToPorPtrs,
            SectorToPicked = locationData.SectorToPicked,
            PickedToSector = path.PickedToSector,
            PortalSequence = path.PortalSequence,
            PortalSequenceBorders = path.PortalSequenceBorders,
            WinToSecPtrs = pickedFieldGraph.WinToSecPtrs,
            PortalNodes = pickedFieldGraph.PortalNodes,
            WindowNodes = pickedFieldGraph.WindowNodes,
            ActiveWaveFrontListArray = path.ActivePortalList,
            NotActivatedPortals = path.NotActivePortalList,
            SectorStateTable = sectorStateTable,
            NewSectorStartIndex = path.NewPickedSectorStartIndex,
        };
        JobHandle submitHandle = submitJob.Schedule();
        pathInfo.Handle = submitHandle;
        if (FlowFieldUtilities.DebugMode) { submitHandle.Complete(); }
        return pathInfo;
    }
}
