using Unity.Jobs;

internal class ActivePortalSubmissionScheduler
{
    PathfindingManager _pathfindingManager;
    PathDataContainer _pathContainer;

    internal ActivePortalSubmissionScheduler(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
        _pathContainer = pathfindingManager.PathContainer;
    }

    internal RequestPipelineInfoWithHandle ScheduleActivePortalSubmission(RequestPipelineInfoWithHandle reqInfo)
    {
        PathfindingInternalData internalData = _pathContainer.PathfindingInternalDataList[reqInfo.PathIndex];
        PathDestinationData destinationData = _pathContainer.PathDestinationDataList[reqInfo.PathIndex];
        PathLocationData locationData = _pathContainer.PathLocationDataList[reqInfo.PathIndex];
        PathPortalTraversalData portalTraversalData = _pathContainer.PathPortalTraversalDataList[reqInfo.PathIndex];
        SectorBitArray sectorBitArray = _pathContainer.PathSectorBitArrays[reqInfo.PathIndex];
        FieldGraph pickedFieldGraph = _pathfindingManager.FieldManager.GetFieldGraphWithOffset(destinationData.Offset);

        //ACTIVE WAVE FRONT SUBMISSION
        ActivePortalSubmitJob submitJob = new ActivePortalSubmitJob()
        {
            SectorColAmount = FlowFieldUtilities.SectorColAmount,
            SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
            SectorMatrixRowAmount = FlowFieldUtilities.SectorMatrixRowAmount,
            SectorRowAmount = FlowFieldUtilities.SectorRowAmount,
            SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
            FieldColAmount = FlowFieldUtilities.FieldColAmount,
            TargetIndex2D = FlowFieldUtilities.PosTo2D(destinationData.Destination, FlowFieldUtilities.TileSize),

            PortalEdges = pickedFieldGraph.PorToPorPtrs,
            SectorToPicked = locationData.SectorToPicked,
            PickedToSector = internalData.PickedSectorList,
            PortalSequence = portalTraversalData.PortalSequence,
            PortalSequenceBorders = portalTraversalData.PortalSequenceBorders,
            WinToSecPtrs = pickedFieldGraph.WinToSecPtrs,
            PortalNodes = pickedFieldGraph.PortalNodes,
            WindowNodes = pickedFieldGraph.WindowNodes,
            ActiveWaveFrontListArray = internalData.ActivePortalList,
            NotActivatedPortals = internalData.NotActivePortalList,
            SectorBitArray = sectorBitArray,
        };
        JobHandle submitHandle = submitJob.Schedule();

        if (FlowFieldUtilities.DebugMode) { submitHandle.Complete(); }

        reqInfo.Handle = submitHandle;
        return reqInfo;
    }
}
