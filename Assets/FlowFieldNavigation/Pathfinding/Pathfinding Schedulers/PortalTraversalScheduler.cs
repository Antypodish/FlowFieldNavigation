using Codice.Client.BaseCommands.Download;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using System.Diagnostics;

namespace FlowFieldNavigation
{
    internal class PortalTraversalScheduler
    {
        FlowFieldNavigationManager _navigationManager;
        PathDataContainer _pathContainer;
        RequestedSectorCalculationScheduler _requestedSectorCalculationScheduler;
        PortalTraversalDataProvider _porTravDataProvider;

        NativeArray<float2> Sources;
        NativeList<PathPipelineReq> PipelineRequests;
        internal PortalTraversalScheduler(FlowFieldNavigationManager navManager, RequestedSectorCalculationScheduler reqSecCalcScheduler, PortalTraversalDataProvider porTravDataProvider)
        {
            PipelineRequests = new NativeList<PathPipelineReq>(Allocator.Persistent);
            _navigationManager = navManager;
            _pathContainer = _navigationManager.PathDataContainer;
            _requestedSectorCalculationScheduler = reqSecCalcScheduler;
            _porTravDataProvider = porTravDataProvider;
        }
        internal void DisposeAll()
        {
            if (PipelineRequests.IsCreated) { PipelineRequests.Dispose(); }
            _requestedSectorCalculationScheduler.DisposeAll();
            _requestedSectorCalculationScheduler = null;
        }
        internal void SetSources(NativeArray<float2> sources) { Sources = sources; }
        internal void SchedulePortalTraversalFor(int pathIndex, Slice pathReqSourceSlice, Slice flowReqSourceSlice, DynamicDestinationState dynamicDestinationState)
        {
            PathfindingInternalData pathInternalData = _navigationManager.PathDataContainer.PathfindingInternalDataList[pathIndex];
            PathDestinationData destinationData = _pathContainer.PathDestinationDataList[pathIndex];
            PathPortalTraversalData portalTraversalData = _pathContainer.PathPortalTraversalDataList[pathIndex];
            PathLocationData locationData = _pathContainer.PathLocationDataList[pathIndex];
            UnsafeList<PathSectorState> sectorStateTable = _pathContainer.PathSectorStateTableList[pathIndex];
            NativeArray<OverlappingDirection> sectorOverlappingDirectionTable = _pathContainer.SectorOverlappingDirectionTableList[pathIndex];
            SectorBitArray sectorBitArray = _pathContainer.PathSectorBitArrays[pathIndex];
            int2 destinationIndex = FlowFieldUtilities.PosTo2D(destinationData.Destination, FlowFieldUtilities.TileSize, FlowFieldUtilities.FieldGridStartPosition);
            CostField pickedCostField = _navigationManager.FieldDataContainer.GetCostFieldWithOffset(destinationData.Offset);
            FieldGraph pickedFieldGraph = _navigationManager.FieldDataContainer.GetFieldGraphWithOffset(destinationData.Offset);
            portalTraversalData.PathAdditionSequenceSliceStartIndex.Value = portalTraversalData.PortalSequenceSlices.Length;
            portalTraversalData.NewPickedSectorStartIndex.Value = pathInternalData.PickedSectorList.Length;

            NativeArray<PortalTraversalData> porTravDataArray = _porTravDataProvider.GetAvailableData(out JobHandle dependency);
            NativeSlice<float2> pathRequestSource = new NativeSlice<float2>(Sources, pathReqSourceSlice.Index, pathReqSourceSlice.Count);
            NativeSlice<float2> flowRequestSource = new NativeSlice<float2>(Sources, flowReqSourceSlice.Index, flowReqSourceSlice.Count);

            //Graph Reduction
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
            JobHandle reductHandle = reductionJob.Schedule(dependency);
            if (FlowFieldUtilities.DebugMode) { reductHandle.Complete(); }

            //Graph Traversal
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
            JobHandle travHandle = traversalJob.Schedule(reductHandle);
            _porTravDataProvider.IncerimentPointer(travHandle);
            if (FlowFieldUtilities.DebugMode) { travHandle.Complete(); }

            //Active wave front submission
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
                PickedSectorIndicies = pathInternalData.PickedSectorList,
                PortalSequence = portalTraversalData.PortalSequence,
                PortalSequenceSlices = portalTraversalData.PortalSequenceSlices,
                WinToSecPtrs = pickedFieldGraph.WinToSecPtrs,
                PortalNodes = pickedFieldGraph.PortalNodes,
                WindowNodes = pickedFieldGraph.WindowNodes,
                SectorToWaveFrontsMap = pathInternalData.SectorToWaveFrontsMap,
                NotActivatedPortals = pathInternalData.NotActivePortalList,
                SectorStateTable = sectorStateTable,
                NewSectorStartIndex = portalTraversalData.NewPickedSectorStartIndex,
                SectorBitArray = sectorBitArray,
                SectorOverlappingDirectionTable = sectorOverlappingDirectionTable,
            };
            JobHandle activeFrontSubmissionHandle = submitJob.Schedule(travHandle);
            if (FlowFieldUtilities.DebugMode) { activeFrontSubmissionHandle.Complete(); }

            _requestedSectorCalculationScheduler.ScheduleRequestedSectorCalculation(pathIndex, activeFrontSubmissionHandle, dynamicDestinationState, flowRequestSource);
            
            /*
            PathPipelineReq pipReq = new PathPipelineReq()
            {
                DynamicDestinationState = dynamicDestinationState,
                FlowReqSourceSlice = flowReqSourceSlice,
                Handle = activeFrontSubmissionHandle,
                PathIndex = pathIndex,
            };
            PipelineRequests.Add(pipReq);*/
        }
    }

}