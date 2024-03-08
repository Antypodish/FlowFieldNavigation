using Codice.Client.BaseCommands.Download;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace FlowFieldNavigation
{

    internal class PortalTraversalScheduler
    {
        FlowFieldNavigationManager _navigationManager;
        PathDataContainer _pathContainer;
        ActivePortalSubmissionScheduler _activePortalSubmissionScheduler;
        RequestedSectorCalculationScheduler _requestedSectorCalculationScheduler;

        NativeList<RequestPipelineInfoWithHandle> ScheduledPortalTraversals;
        internal PortalTraversalScheduler(FlowFieldNavigationManager navigationManager, RequestedSectorCalculationScheduler requestedSectorCalculationScheduler)
        {
            ScheduledPortalTraversals = new NativeList<RequestPipelineInfoWithHandle>(Allocator.Persistent);
            _navigationManager = navigationManager;
            _pathContainer = _navigationManager.PathDataContainer;
            _activePortalSubmissionScheduler = new ActivePortalSubmissionScheduler(navigationManager);
            _requestedSectorCalculationScheduler = requestedSectorCalculationScheduler;
        }
        internal void DisposeAll()
        {
            if (ScheduledPortalTraversals.IsCreated) { ScheduledPortalTraversals.Dispose(); }
            _requestedSectorCalculationScheduler.DisposeAll();
            _activePortalSubmissionScheduler = null;
            _requestedSectorCalculationScheduler = null;
        }

        internal void SchedulePortalTraversalFor(RequestPipelineInfoWithHandle reqInfo, NativeSlice<float2> sources)
        {
            PathfindingInternalData pathInternalData = _navigationManager.PathDataContainer.PathfindingInternalDataList[reqInfo.PathIndex];
            PathDestinationData destinationData = _pathContainer.PathDestinationDataList[reqInfo.PathIndex];
            PathLocationData locationData = _pathContainer.PathLocationDataList[reqInfo.PathIndex];
            PathPortalTraversalData portalTraversalData = _pathContainer.PathPortalTraversalDataList[reqInfo.PathIndex];
            UnsafeList<PathSectorState> sectorStateTable = _pathContainer.PathSectorStateTableList[reqInfo.PathIndex];
            int2 destinationIndex = FlowFieldUtilities.PosTo2D(destinationData.Destination, FlowFieldUtilities.TileSize, FlowFieldUtilities.FieldGridStartPosition);
            CostField pickedCostField = _navigationManager.FieldDataContainer.GetCostFieldWithOffset(destinationData.Offset);
            FieldGraph pickedFieldGraph = _navigationManager.FieldDataContainer.GetFieldGraphWithOffset(destinationData.Offset);
            portalTraversalData.PathAdditionSequenceBorderStartIndex.Value = 0;
            portalTraversalData.NewPickedSectorStartIndex.Value = pathInternalData.PickedSectorList.Length;

            PortalReductionJob reductionJob = new PortalReductionJob()
            {
                TargetIndex = destinationIndex,
                FieldColAmount = FlowFieldUtilities.FieldColAmount,
                FieldRowAmount = FlowFieldUtilities.FieldRowAmount,
                FieldTileSize = FlowFieldUtilities.TileSize,
                SectorColAmount = FlowFieldUtilities.SectorColAmount,
                SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
                SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
                FieldGridStartPos = FlowFieldUtilities.FieldGridStartPosition,
                PickedToSector = pathInternalData.PickedSectorList,
                TargetSectorCosts = _pathContainer.TargetSectorIntegrationList[reqInfo.PathIndex],
                PortalNodes = pickedFieldGraph.PortalNodes,
                SecToWinPtrs = pickedFieldGraph.SecToWinPtrs,
                WindowNodes = pickedFieldGraph.WindowNodes,
                WinToSecPtrs = pickedFieldGraph.WinToSecPtrs,
                SourcePositions = sources,
                PorPtrs = pickedFieldGraph.PorToPorPtrs,
                SectorNodes = pickedFieldGraph.SectorNodes,
                Costs = pickedCostField.Costs,
                LocalDirections = _navigationManager.FieldDataContainer.GetSectorDirections(),
                SectorToPicked = locationData.SectorToPicked,
                PortalTraversalDataArray = portalTraversalData.PortalTraversalDataArray,
                SourcePortalIndexList = portalTraversalData.SourcePortalIndexList,
                TargetNeighbourPortalIndicies = portalTraversalData.TargetSectorPortalIndexList,
                AStarTraverseIndexList = portalTraversalData.AStartTraverseIndexList,
                IslandFields = pickedFieldGraph.IslandFields,
                SectorStateTable = sectorStateTable,
                DijkstraStartIndicies = portalTraversalData.DiskstraStartIndicies,
                FlowFieldLength = pathInternalData.FlowFieldLength,
            };

            //TRAVERSAL
            PortalTraversalJob traversalJob = new PortalTraversalJob()
            {
                FieldColAmount = FlowFieldUtilities.FieldColAmount,
                Target = destinationIndex,
                SectorColAmount = FlowFieldUtilities.SectorColAmount,
                SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
                SectorMatrixRowAmount = FlowFieldUtilities.SectorMatrixRowAmount,
                SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
                LOSRange = FlowFieldUtilities.LOSRange,
                DijkstraStartIndicies = portalTraversalData.DiskstraStartIndicies,
                SectorWithinLOSState = pathInternalData.SectorWithinLOSState,
                NewPickedSectorStartIndex = portalTraversalData.NewPickedSectorStartIndex,
                AddedPortalSequenceBorderStartIndex = portalTraversalData.PathAdditionSequenceBorderStartIndex.Value,
                PickedToSector = pathInternalData.PickedSectorList,
                PortalSequenceBorders = portalTraversalData.PortalSequenceBorders,
                PortalNodes = pickedFieldGraph.PortalNodes,
                WindowNodes = pickedFieldGraph.WindowNodes,
                WinToSecPtrs = pickedFieldGraph.WinToSecPtrs,
                PorPtrs = pickedFieldGraph.PorToPorPtrs,
                SectorNodes = pickedFieldGraph.SectorNodes,
                PortalSequence = portalTraversalData.PortalSequence,
                SectorToPicked = locationData.SectorToPicked,
                FlowFieldLength = pathInternalData.FlowFieldLength,
                PortalTraversalDataArray = portalTraversalData.PortalTraversalDataArray,
                TargetNeighbourPortalIndicies = portalTraversalData.TargetSectorPortalIndexList,
                SectorStateTable = sectorStateTable,
                SourcePortalIndexList = portalTraversalData.SourcePortalIndexList,                
            };

            JobHandle reductHandle = reductionJob.Schedule();
            JobHandle travHandle = traversalJob.Schedule(reductHandle);
            if (FlowFieldUtilities.DebugMode) { travHandle.Complete(); }

            reqInfo.Handle = travHandle;
            ScheduledPortalTraversals.Add(reqInfo);
        }

        internal void TryComplete(NativeList<FinalPathRequest> requestedPaths, NativeArray<float2> sources)
        {
            for (int i = ScheduledPortalTraversals.Length - 1; i >= 0; i--)
            {
                RequestPipelineInfoWithHandle reqInfo = ScheduledPortalTraversals[i];
                if (reqInfo.Handle.IsCompleted)
                {
                    reqInfo.Handle.Complete();
                    _pathContainer.FinalizePathBuffers(reqInfo.PathIndex);

                    //SCHEDULE ACTIVE PORTAL SUBMISSION
                    RequestPipelineInfoWithHandle portalSubmissionReqInfo = _activePortalSubmissionScheduler.ScheduleActivePortalSubmission(reqInfo);
                    PathPipelineInfoWithHandle portalSubmissionPathInfo = portalSubmissionReqInfo.ToPathPipelineInfoWithHandle();
                    //SCHEDULE REQUESTED SECTOR CALCULATION
                    FinalPathRequest pathReq = requestedPaths[reqInfo.RequestIndex];
                    NativeSlice<float2> sourcePositions = new NativeSlice<float2>(sources, pathReq.SourcePositionStartIndex, pathReq.SourceCount);
                    _requestedSectorCalculationScheduler.ScheduleRequestedSectorCalculation(portalSubmissionPathInfo, portalSubmissionPathInfo.Handle, sourcePositions);

                    ScheduledPortalTraversals.RemoveAtSwapBack(i);
                }
            }
        }
        internal void ForceComplete(NativeList<FinalPathRequest> requestedPaths, NativeArray<float2> sources)
        {
            for (int i = ScheduledPortalTraversals.Length - 1; i >= 0; i--)
            {
                RequestPipelineInfoWithHandle reqInfo = ScheduledPortalTraversals[i];
                reqInfo.Handle.Complete();
                _pathContainer.FinalizePathBuffers(reqInfo.PathIndex);

                //SCHEDULE ACTIVE PORTAL SUBMISSION
                RequestPipelineInfoWithHandle portalSubmissionReqInfo = _activePortalSubmissionScheduler.ScheduleActivePortalSubmission(reqInfo);
                PathPipelineInfoWithHandle portalSubmissionPathInfo = portalSubmissionReqInfo.ToPathPipelineInfoWithHandle();
                //SCHEDULE REQUESTED SECTOR CALCULATION
                FinalPathRequest pathReq = requestedPaths[reqInfo.RequestIndex];
                NativeSlice<float2> sourcePositions = new NativeSlice<float2>(sources, pathReq.SourcePositionStartIndex, pathReq.SourceCount);
                _requestedSectorCalculationScheduler.ScheduleRequestedSectorCalculation(portalSubmissionPathInfo, portalSubmissionPathInfo.Handle, sourcePositions);
            }
            ScheduledPortalTraversals.Clear();
        }
    }

}