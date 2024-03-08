using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;


namespace FlowFieldNavigation
{
    internal class AdditionActivePortalSubmissionScheduler
    {
        FlowFieldNavigationManager _navigationManager;
        PathDataContainer _pathContainer;

        internal AdditionActivePortalSubmissionScheduler(FlowFieldNavigationManager navigationManager)
        {
            _navigationManager = navigationManager;
            _pathContainer = navigationManager.PathDataContainer;
        }
        internal PathPipelineInfoWithHandle ScheduleActivePortalSubmission(PathPipelineInfoWithHandle pathInfo)
        {
            PathfindingInternalData internalData = _pathContainer.PathfindingInternalDataList[pathInfo.PathIndex];
            PathDestinationData destinationData = _pathContainer.PathDestinationDataList[pathInfo.PathIndex];
            PathLocationData locationData = _pathContainer.PathLocationDataList[pathInfo.PathIndex];
            PathPortalTraversalData portalTraversalData = _pathContainer.PathPortalTraversalDataList[pathInfo.PathIndex];
            UnsafeList<PathSectorState> sectorStateTable = _pathContainer.PathSectorStateTableList[pathInfo.PathIndex];
            SectorBitArray sectorBitArray = _pathContainer.PathSectorBitArrays[pathInfo.PathIndex];
            FieldGraph pickedFieldGraph = _navigationManager.FieldDataContainer.GetFieldGraphWithOffset(destinationData.Offset);
            int newSecorCount = internalData.PickedSectorList.Length - internalData.ActivePortalList.Length;
            _pathContainer.ResizeActiveWaveFrontList(newSecorCount, internalData.ActivePortalList);

            //ACTIVE WAVE FRONT SUBMISSION
            NewActivePortalSubmitJob submitJob = new NewActivePortalSubmitJob()
            {
                SectorColAmount = FlowFieldUtilities.SectorColAmount,
                SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
                SectorMatrixRowAmount = FlowFieldUtilities.SectorMatrixRowAmount,
                SectorRowAmount = FlowFieldUtilities.SectorRowAmount,
                SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
                FieldColAmount = FlowFieldUtilities.FieldColAmount,
                TargetIndex2D = FlowFieldUtilities.PosTo2D(destinationData.Destination, FlowFieldUtilities.TileSize, FlowFieldUtilities.FieldGridStartPosition),
                SequenceBorderListStartIndex = portalTraversalData.PathAdditionSequenceBorderStartIndex.Value,

                PortalEdges = pickedFieldGraph.PorToPorPtrs,
                SectorToPicked = locationData.SectorToPicked,
                PickedToSector = internalData.PickedSectorList.AsArray(),
                PortalSequence = portalTraversalData.PortalSequence.AsArray(),
                PortalSequenceBorders = portalTraversalData.PortalSequenceBorders.AsArray(),
                WinToSecPtrs = pickedFieldGraph.WinToSecPtrs,
                PortalNodes = pickedFieldGraph.PortalNodes,
                WindowNodes = pickedFieldGraph.WindowNodes,
                ActiveWaveFrontListArray = internalData.ActivePortalList.AsArray(),
                NotActivatedPortals = internalData.NotActivePortalList,
                SectorStateTable = sectorStateTable,
                NewSectorStartIndex = portalTraversalData.NewPickedSectorStartIndex,
                SectorBitArray = sectorBitArray,
            };
            JobHandle submitHandle = submitJob.Schedule();
            pathInfo.Handle = submitHandle;
            if (FlowFieldUtilities.DebugMode) { submitHandle.Complete(); }
            return pathInfo;
        }
    }


}