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

        internal JobHandle ScheduleActivePortalSubmission(int pathIndex)
        {
            PathfindingInternalData internalData = _pathContainer.PathfindingInternalDataList[pathIndex];
            PathDestinationData destinationData = _pathContainer.PathDestinationDataList[pathIndex];
            PathLocationData locationData = _pathContainer.PathLocationDataList[pathIndex];
            PathPortalTraversalData portalTraversalData = _pathContainer.PathPortalTraversalDataList[pathIndex];
            SectorBitArray sectorBitArray = _pathContainer.PathSectorBitArrays[pathIndex];
            UnsafeList<PathSectorState> sectorStateTable = _pathContainer.PathSectorStateTableList[pathIndex];
            NativeArray<OverlappingDirection> sectorOverlappingDirectionTable = _pathContainer.SectorOverlappingDirectionTableList[pathIndex];
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
                SequenceSliceListStartIndex = portalTraversalData.PathAdditionSequenceSliceStartIndex.Value,

                PortalEdges = pickedFieldGraph.PorToPorPtrs,
                SectorToPicked = locationData.SectorToPicked,
                PickedToSector = internalData.PickedSectorList.AsArray(),
                PortalSequence = portalTraversalData.PortalSequence.AsArray(),
                PortalSequenceSlices = portalTraversalData.PortalSequenceSlices.AsArray(),
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
            return submitHandle;
        }
    }


}