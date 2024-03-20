using Codice.Client.BaseCommands.Download;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using System.Diagnostics;

namespace FlowFieldNavigation
{
    internal class NewPortalTraversalScheduler
    {
        FlowFieldNavigationManager _navigationManager;
        PathDataContainer _pathContainer;
        ActivePortalSubmissionScheduler _activePortalSubmissionScheduler;
        RequestedSectorCalculationScheduler _requestedSectorCalculationScheduler;
        PortalTraversalDataProvider _porTravDataProvider;

        NativeArray<float2> Sources;
        NativeList<PathPipelineReq> PipelineRequests;
        internal NewPortalTraversalScheduler(FlowFieldNavigationManager navManager, RequestedSectorCalculationScheduler reqSecCalcScheduler, PortalTraversalDataProvider porTravDataProvider)
        {
            PipelineRequests = new NativeList<PathPipelineReq>(Allocator.Persistent);
            _navigationManager = navManager;
            _pathContainer = _navigationManager.PathDataContainer;
            _activePortalSubmissionScheduler = new ActivePortalSubmissionScheduler(navManager);
            _requestedSectorCalculationScheduler = reqSecCalcScheduler;
            _porTravDataProvider = porTravDataProvider;
        }
        internal void DisposeAll()
        {
            if (PipelineRequests.IsCreated) { PipelineRequests.Dispose(); }
            _requestedSectorCalculationScheduler.DisposeAll();
            _activePortalSubmissionScheduler = null;
            _requestedSectorCalculationScheduler = null;
        }
        internal void SetSources(NativeArray<float2> sources) { Sources = sources; }
        internal void SchedulePortalTraversalFor(int pathIndex, Slice pathReqSourceSlice, Slice flowReqSourceSlice, DynamicDestinationState dynamicDestinationState)
        {
            PathfindingInternalData pathInternalData = _navigationManager.PathDataContainer.PathfindingInternalDataList[pathIndex];
            PathDestinationData destinationData = _pathContainer.PathDestinationDataList[pathIndex];
            PathPortalTraversalData portalTraversalData = _pathContainer.PathPortalTraversalDataList[pathIndex];
            UnsafeList<PathSectorState> sectorStateTable = _pathContainer.PathSectorStateTableList[pathIndex];
            int2 destinationIndex = FlowFieldUtilities.PosTo2D(destinationData.Destination, FlowFieldUtilities.TileSize, FlowFieldUtilities.FieldGridStartPosition);
            CostField pickedCostField = _navigationManager.FieldDataContainer.GetCostFieldWithOffset(destinationData.Offset);
            FieldGraph pickedFieldGraph = _navigationManager.FieldDataContainer.GetFieldGraphWithOffset(destinationData.Offset);
            portalTraversalData.PathAdditionSequenceSliceStartIndex.Value = portalTraversalData.PortalSequenceSlices.Length;
            portalTraversalData.NewPickedSectorStartIndex.Value = pathInternalData.PickedSectorList.Length;

            NativeArray<PortalTraversalData> porTravDataArray = _porTravDataProvider.GetAvailableData(out JobHandle dependency);
            NativeSlice<float2> pathRequestSource = new NativeSlice<float2>(Sources, pathReqSourceSlice.Index, pathReqSourceSlice.Count);
            PortalReductionJob reductionJob = new PortalReductionJob()
            {
                TileSize = FlowFieldUtilities.TileSize,
                TargetIndex = destinationIndex,
                FieldColAmount = FlowFieldUtilities.FieldColAmount,
                FieldRowAmount = FlowFieldUtilities.FieldRowAmount,
                FieldTileSize = FlowFieldUtilities.TileSize,
                SectorColAmount = FlowFieldUtilities.SectorColAmount,
                SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
                SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
                FieldGridStartPos = FlowFieldUtilities.FieldGridStartPosition,
                PickedToSector = pathInternalData.PickedSectorList,
                TargetSectorCosts = _pathContainer.TargetSectorIntegrationList[pathIndex],
                PortalNodes = pickedFieldGraph.PortalNodes,
                SecToWinPtrs = pickedFieldGraph.SecToWinPtrs,
                WindowNodes = pickedFieldGraph.WindowNodes,
                WinToSecPtrs = pickedFieldGraph.WinToSecPtrs,
                SourcePositions = pathRequestSource,
                PorPtrs = pickedFieldGraph.PorToPorPtrs,
                SectorNodes = pickedFieldGraph.SectorNodes,
                Costs = pickedCostField.Costs,
                LocalDirections = _navigationManager.FieldDataContainer.GetSectorDirections(),
                PortalTraversalDataArray = porTravDataArray,
                SourcePortalIndexList = portalTraversalData.SourcePortalIndexList,
                IslandFields = pickedFieldGraph.IslandFields,
                SectorStateTable = sectorStateTable,
                DijkstraStartIndicies = portalTraversalData.DiskstraStartIndicies,
                GoalTraversalDataList = portalTraversalData.GoalDataList,
                NewReducedNodeIndicies = portalTraversalData.NewReducedPortalIndicies,
                PortalDataRecords = portalTraversalData.PortalDataRecords.AsArray(),
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
                NewPortalSliceStartIndex = portalTraversalData.PathAdditionSequenceSliceStartIndex.Value,
                PickedSectorIndicies = pathInternalData.PickedSectorList,
                PortalSequenceSlices = portalTraversalData.PortalSequenceSlices,
                PortalNodes = pickedFieldGraph.PortalNodes,
                WindowNodes = pickedFieldGraph.WindowNodes,
                WinToSecPtrs = pickedFieldGraph.WinToSecPtrs,
                PorPtrs = pickedFieldGraph.PorToPorPtrs,
                SectorNodes = pickedFieldGraph.SectorNodes,
                PortalSequence = portalTraversalData.PortalSequence,
                FlowFieldLength = pathInternalData.FlowFieldLength,
                PortalTraversalDataArray = porTravDataArray,
                SectorStateTable = sectorStateTable,
                SourcePortalIndexList = portalTraversalData.SourcePortalIndexList,
                NewReducedPortalIndicies = portalTraversalData.NewReducedPortalIndicies,
                PortalDataRecords = portalTraversalData.PortalDataRecords,
            };
            JobHandle reductHandle = reductionJob.Schedule(dependency);
            JobHandle travHandle = traversalJob.Schedule(reductHandle);
            if (FlowFieldUtilities.DebugMode) { travHandle.Complete(); }
            _porTravDataProvider.IncerimentPointer(travHandle);

            PathPipelineReq pipReq = new PathPipelineReq()
            {
                DynamicDestinationState = dynamicDestinationState,
                FlowReqSourceSlice = flowReqSourceSlice,
                Handle = travHandle,
                PathIndex = pathIndex,
            };
            PipelineRequests.Add(pipReq);
        }

        internal void TryComplete()
        {
            for (int i = PipelineRequests.Length - 1; i >= 0; i--)
            {
                PathPipelineReq pipReq = PipelineRequests[i];
                if (pipReq.Handle.IsCompleted)
                {
                    pipReq.Handle.Complete();
                    pipReq.Handle = _activePortalSubmissionScheduler.ScheduleActivePortalSubmission(pipReq.PathIndex, pipReq.Handle);
                    NativeSlice<float2> sourcePositions = new NativeSlice<float2>(Sources, pipReq.FlowReqSourceSlice.Index, pipReq.FlowReqSourceSlice.Count);
                    _requestedSectorCalculationScheduler.ScheduleRequestedSectorCalculation(pipReq.PathIndex, pipReq.Handle, pipReq.DynamicDestinationState, sourcePositions);
                    PipelineRequests.RemoveAtSwapBack(i);
                }
            }
        }
        internal void ForceComplete(NativeList<FinalPathRequest> requestedPaths, NativeArray<float2> sources)
        {
            for (int i = PipelineRequests.Length - 1; i >= 0; i--)
            {
                PathPipelineReq pipReq = PipelineRequests[i];
                pipReq.Handle.Complete();
                pipReq.Handle = _activePortalSubmissionScheduler.ScheduleActivePortalSubmission(pipReq.PathIndex, pipReq.Handle);
                NativeSlice<float2> sourcePositions = new NativeSlice<float2>(Sources, pipReq.FlowReqSourceSlice.Index, pipReq.FlowReqSourceSlice.Count);
                _requestedSectorCalculationScheduler.ScheduleRequestedSectorCalculation(pipReq.PathIndex, pipReq.Handle, pipReq.DynamicDestinationState, sourcePositions);
                PipelineRequests.RemoveAtSwapBack(i);
            }
            PipelineRequests.Clear();
        }
    }

}