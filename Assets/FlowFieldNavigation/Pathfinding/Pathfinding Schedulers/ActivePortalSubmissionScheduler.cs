using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Collections;

namespace FlowFieldNavigation
{
    internal class ActivePortalSubmissionScheduler
    {
        FlowFieldNavigationManager _navigationManager;
        PathDataContainer _pathContainer;

        internal ActivePortalSubmissionScheduler(FlowFieldNavigationManager navigationManager)
        {
            _navigationManager = navigationManager;
            _pathContainer = navigationManager.PathDataContainer;
        }

        internal RequestPipelineInfoWithHandle ScheduleActivePortalSubmission(RequestPipelineInfoWithHandle reqInfo)
        {
            PathfindingInternalData internalData = _pathContainer.PathfindingInternalDataList[reqInfo.PathIndex];
            PathDestinationData destinationData = _pathContainer.PathDestinationDataList[reqInfo.PathIndex];
            PathLocationData locationData = _pathContainer.PathLocationDataList[reqInfo.PathIndex];
            PathPortalTraversalData portalTraversalData = _pathContainer.PathPortalTraversalDataList[reqInfo.PathIndex];
            SectorBitArray sectorBitArray = _pathContainer.PathSectorBitArrays[reqInfo.PathIndex];
            UnsafeList<PathSectorState> sectorStateTable = _pathContainer.PathSectorStateTableList[reqInfo.PathIndex];
            NativeArray<OverlappingDirection> sectorOverlappingDirectionTable = _pathContainer.SectorOverlappingDirectionTableList[reqInfo.PathIndex];
            FieldGraph pickedFieldGraph = _navigationManager.FieldDataContainer.GetFieldGraphWithOffset(destinationData.Offset);

            //ACTIVE WAVE FRONT SUBMISSION
            ActivePortalSubmitJob submitJob = new ActivePortalSubmitJob()
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
                SectorToWaveFrontsMap = internalData.SectorToWaveFrontsMap,
                NotActivatedPortals = internalData.NotActivePortalList,
                SectorStateTable = sectorStateTable,
                NewSectorStartIndex = portalTraversalData.NewPickedSectorStartIndex,
                SectorBitArray = sectorBitArray,
                SectorOverlappingDirectionTable = sectorOverlappingDirectionTable,
            };
            JobHandle submitHandle = submitJob.Schedule();

            if (FlowFieldUtilities.DebugMode) { submitHandle.Complete(); }

            reqInfo.Handle = submitHandle;
            return reqInfo;
        }
    }


}