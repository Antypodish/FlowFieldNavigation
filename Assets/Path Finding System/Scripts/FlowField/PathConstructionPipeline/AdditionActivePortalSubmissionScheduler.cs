using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

internal class AdditionActivePortalSubmissionScheduler
{
    PathfindingManager _pathfindingManager;
    PathContainer _pathProducer;

    public AdditionActivePortalSubmissionScheduler(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
        _pathProducer = pathfindingManager.PathProducer;
    }

    public PathPipelineInfoWithHandle ScheduleActivePortalSubmission(PathPipelineInfoWithHandle pathInfo)
    {
        Path path = _pathProducer.ProducedPaths[pathInfo.PathIndex];
        FieldGraph pickedFieldGraph = _pathfindingManager.FieldProducer.GetFieldGraphWithOffset(path.Offset);
        int newSecorCount = path.PickedToSector.Length - path.ActiveWaveFrontList.Length;
        _pathProducer.ResizeActiveWaveFrontList(newSecorCount, path.ActiveWaveFrontList);

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
            SectorToPicked = path.SectorToPicked,
            PickedToSector = path.PickedToSector,
            PortalSequence = path.PortalSequence,
            PortalSequenceBorders = path.PortalSequenceBorders,
            WinToSecPtrs = pickedFieldGraph.WinToSecPtrs,
            PortalNodes = pickedFieldGraph.PortalNodes,
            WindowNodes = pickedFieldGraph.WindowNodes,
            ActiveWaveFrontListArray = path.ActiveWaveFrontList,
            NotActivatedPortals = path.NotActivePortalList,
            SectorStateTable = path.SectorStateTable,
            NewSectorStartIndex = path.NewPickedSectorStartIndex,
        };
        JobHandle submitHandle = submitJob.Schedule();
        pathInfo.Handle = submitHandle;
        if (FlowFieldUtilities.DebugMode) { submitHandle.Complete(); }
        return pathInfo;
    }
}
