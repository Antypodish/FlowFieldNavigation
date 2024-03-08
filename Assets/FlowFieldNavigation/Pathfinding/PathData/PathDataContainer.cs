using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace FlowFieldNavigation
{
    internal class PathDataContainer
    {
        internal NativeList<PathFlowData> ExposedPathFlowData;
        internal NativeList<PathLocationData> ExposedPathLocationData;
        internal NativeList<float2> ExposedPathDestinations;
        internal NativeList<int> ExposedPathFlockIndicies;
        internal NativeList<float> ExposedPathReachDistanceCheckRanges;
        internal NativeList<PathState> ExposedPathStateList;
        internal NativeList<bool> ExposedPathAgentStopFlagList;
        internal List<PathfindingInternalData> PathfindingInternalDataList;
        internal NativeList<PathLocationData> PathLocationDataList;
        internal NativeList<PathFlowData> PathFlowDataList;
        internal NativeList<UnsafeList<PathSectorState>> PathSectorStateTableList;
        internal NativeList<PathDestinationData> PathDestinationDataList;
        internal NativeList<UnsafeList<DijkstraTile>> TargetSectorIntegrationList;
        internal NativeList<PathRoutineData> PathRoutineDataList;
        internal NativeList<SectorBitArray> PathSectorBitArrays;
        internal List<PathPortalTraversalData> PathPortalTraversalDataList;
        internal NativeList<int> PathFlockIndicies;
        internal NativeList<int> PathSubscriberCounts;
        internal List<NativeArray<OverlappingDirection>> SectorOverlappingDirectionTableList;
        Stack<int> _removedPathIndicies;

        FieldDataContainer _fieldProducer;
        PathPreallocator _preallocator;
        internal PathDataContainer(FlowFieldNavigationManager navigationManager)
        {
            _fieldProducer = navigationManager.FieldDataContainer;
            PathfindingInternalDataList = new List<PathfindingInternalData>(1);
            _preallocator = new PathPreallocator(_fieldProducer, FlowFieldUtilities.SectorTileAmount, FlowFieldUtilities.SectorMatrixTileAmount);
            _removedPathIndicies = new Stack<int>();
            PathSubscriberCounts = new NativeList<int>(Allocator.Persistent);
            PathLocationDataList = new NativeList<PathLocationData>(1, Allocator.Persistent);
            PathFlowDataList = new NativeList<PathFlowData>(Allocator.Persistent);
            PathSectorStateTableList = new NativeList<UnsafeList<PathSectorState>>(Allocator.Persistent);
            PathPortalTraversalDataList = new List<PathPortalTraversalData>();
            PathDestinationDataList = new NativeList<PathDestinationData>(Allocator.Persistent);
            ExposedPathStateList = new NativeList<PathState>(Allocator.Persistent);
            TargetSectorIntegrationList = new NativeList<UnsafeList<DijkstraTile>>(Allocator.Persistent);
            PathRoutineDataList = new NativeList<PathRoutineData>(Allocator.Persistent);
            PathSectorBitArrays = new NativeList<SectorBitArray>(Allocator.Persistent);

            ExposedPathDestinations = new NativeList<float2>(Allocator.Persistent);
            ExposedPathFlowData = new NativeList<PathFlowData>(Allocator.Persistent);
            ExposedPathLocationData = new NativeList<PathLocationData>(Allocator.Persistent);
            PathFlockIndicies = new NativeList<int>(Allocator.Persistent);
            ExposedPathFlockIndicies = new NativeList<int>(Allocator.Persistent);
            ExposedPathReachDistanceCheckRanges = new NativeList<float>(Allocator.Persistent);
            ExposedPathAgentStopFlagList = new NativeList<bool>(Allocator.Persistent);
            SectorOverlappingDirectionTableList = new List<NativeArray<OverlappingDirection>>();
        }
        internal void DisposeAll()
        {
            if (ExposedPathFlowData.IsCreated) { ExposedPathFlowData.Dispose(); }
            if (ExposedPathLocationData.IsCreated) { ExposedPathLocationData.Dispose(); }
            if (ExposedPathDestinations.IsCreated) { ExposedPathDestinations.Dispose(); }
            if (ExposedPathFlockIndicies.IsCreated) { ExposedPathFlockIndicies.Dispose(); }
            if (ExposedPathReachDistanceCheckRanges.IsCreated) { ExposedPathReachDistanceCheckRanges.Dispose(); }
            if (ExposedPathStateList.IsCreated) { ExposedPathStateList.Dispose(); }
            if (ExposedPathAgentStopFlagList.IsCreated) { ExposedPathAgentStopFlagList.Dispose(); }
            if (PathLocationDataList.IsCreated) { PathLocationDataList.Dispose(); }
            if (PathFlowDataList.IsCreated) { PathFlowDataList.Dispose(); }
            if (PathSectorStateTableList.IsCreated) { PathSectorStateTableList.Dispose(); }
            if (PathDestinationDataList.IsCreated) { PathDestinationDataList.Dispose(); }
            if (TargetSectorIntegrationList.IsCreated) { TargetSectorIntegrationList.Dispose(); }
            if (PathRoutineDataList.IsCreated) { PathRoutineDataList.Dispose(); }
            if (PathSectorBitArrays.IsCreated) { PathSectorBitArrays.Dispose(); }
            if (PathFlockIndicies.IsCreated) { PathFlockIndicies.Dispose(); }
            if (PathSubscriberCounts.IsCreated) { PathSubscriberCounts.Dispose(); }
        }
        internal void Update()
        {
            const int maxDeallocationPerFrame = 10;
            int deallcoated = 0;
            for (int i = 0; i < PathfindingInternalDataList.Count; i++)
            {
                PathfindingInternalData internalData = PathfindingInternalDataList[i];
                PathState pathState = ExposedPathStateList[i];
                if (pathState == PathState.Removed) { continue; }
                int subsciriber = PathSubscriberCounts[i];
                if (subsciriber == 0)
                {
                    if(deallcoated >= maxDeallocationPerFrame) { break; }
                    deallcoated++;
                    PathLocationData locationData = PathLocationDataList[i];
                    PathFlowData flowData = PathFlowDataList[i];
                    UnsafeList<PathSectorState> sectorStateTable = PathSectorStateTableList[i];
                    PathPortalTraversalData portalTraversalData = PathPortalTraversalDataList[i];
                    UnsafeList<DijkstraTile> targetSectorIntegration = TargetSectorIntegrationList[i];
                    PathDestinationData destinationData = PathDestinationDataList[i];
                    SectorBitArray sectorBitArray = PathSectorBitArrays[i];
                    NativeArray<OverlappingDirection> sectorOverlappingDirections = SectorOverlappingDirectionTableList[i];
                    sectorOverlappingDirections.Dispose();
                    flowData.Dispose();
                    ExposedPathStateList[i] = PathState.Removed;
                    _removedPathIndicies.Push(i);
                    PreallocationPack preallocations = new PreallocationPack()
                    {
                        SectorToPicked = locationData.SectorToPicked,
                        PickedToSector = internalData.PickedSectorList,
                        PortalSequence = portalTraversalData.PortalSequence,
                        PortalSequenceBorders = portalTraversalData.PortalSequenceBorders,
                        PortalTraversalDataArray = portalTraversalData.PortalTraversalDataArray,
                        TargetSectorCosts = targetSectorIntegration,
                        SourcePortalIndexList = portalTraversalData.SourcePortalIndexList,
                        AStartTraverseIndexList = portalTraversalData.AStartTraverseIndexList,
                        TargetSectorPortalIndexList = portalTraversalData.TargetSectorPortalIndexList,
                        PortalTraversalFastMarchingQueue = internalData.PortalTraversalQueue,
                        SectorStateTable = sectorStateTable,
                        SectorStartIndexListToCalculateFlow = internalData.SectorFlowStartIndiciesToCalculateFlow,
                        SectorStartIndexListToCalculateIntegration = internalData.SectorFlowStartIndiciesToCalculateIntegration,
                        NotActivePortalList = internalData.NotActivePortalList,
                        NewPickedSectorStartIndex = portalTraversalData.NewPickedSectorStartIndex,
                        FlowFieldLength = internalData.FlowFieldLength,
                        PathAdditionSequenceBorderStartIndex = portalTraversalData.PathAdditionSequenceBorderStartIndex,
                        DynamicAreaIntegrationField = internalData.DynamicArea.IntegrationField,
                        DynamicAreaFlowFieldCalculationBuffer = internalData.DynamicArea.FlowFieldCalculationBuffer,
                        DynamicAreaSectorFlowStartCalculationList = internalData.DynamicArea.SectorFlowStartCalculationBuffer,
                        DynamicAreaSectorFlowStartList = locationData.DynamicAreaPickedSectorFlowStarts,
                        DynamicAreaFlowField = flowData.DynamicAreaFlowField,
                        SectorsWithinLOSState = internalData.SectorWithinLOSState,
                        SectorBitArray = sectorBitArray,
                        DijkstraStartIndicies = portalTraversalData.DiskstraStartIndicies,
                    };
                    _preallocator.SendPreallocationsBack(ref preallocations, internalData.ActivePortalList, flowData.FlowField, internalData.IntegrationField, destinationData.Offset);
                }
            }
            _preallocator.CheckForDeallocations();
        }
        internal void ExposeBuffers(NativeArray<int> destinationUpdatedPathIndicies, NativeArray<int> newPathIndicies, NativeArray<int> expandedPathIndicies)
        {
            PathDataExposeJob dataExposeJob = new PathDataExposeJob()
            {
                DestinationUpdatedPathIndicies = destinationUpdatedPathIndicies,
                NewPathIndicies = newPathIndicies,
                ExpandedPathIndicies = expandedPathIndicies,
                ExposedPathDestinationList = ExposedPathDestinations,
                ExposedPathFlowDataList = ExposedPathFlowData,
                ExposedPathLocationList = ExposedPathLocationData,
                ExposedPathFlockIndicies = ExposedPathFlockIndicies,
                ExposedPathReachDistanceCheckRange = ExposedPathReachDistanceCheckRanges,
                PathStopFlagList = ExposedPathAgentStopFlagList,
                PathStateList = ExposedPathStateList,
                PathDestinationDataArray = PathDestinationDataList.AsArray(),
                PathFlowDataArray = PathFlowDataList.AsArray(),
                PathLocationDataArray = PathLocationDataList.AsArray(),
                PathFlockIndicies = PathFlockIndicies.AsArray(),
            };
            dataExposeJob.Schedule().Complete();
        }
        internal int CreatePath(FinalPathRequest request)
        {
            PreallocationPack preallocations = _preallocator.GetPreallocations(request.Offset);

            int pathIndex;
            if (_removedPathIndicies.Count != 0) { pathIndex = _removedPathIndicies.Pop(); }
            else { pathIndex = PathfindingInternalDataList.Count; }

            PathfindingInternalData internalData = new PathfindingInternalData()
            {
                PickedSectorList = preallocations.PickedToSector,
                FlowFieldLength = preallocations.FlowFieldLength,
                PortalTraversalQueue = preallocations.PortalTraversalFastMarchingQueue,
                NotActivePortalList = preallocations.NotActivePortalList,
                SectorFlowStartIndiciesToCalculateIntegration = preallocations.SectorStartIndexListToCalculateIntegration,
                SectorFlowStartIndiciesToCalculateFlow = preallocations.SectorStartIndexListToCalculateFlow,
                SectorWithinLOSState = preallocations.SectorsWithinLOSState,
                DynamicArea = new DynamicArea()
                {
                    FlowFieldCalculationBuffer = preallocations.DynamicAreaFlowFieldCalculationBuffer,
                    IntegrationField = preallocations.DynamicAreaIntegrationField,
                    SectorFlowStartCalculationBuffer = preallocations.DynamicAreaSectorFlowStartCalculationList,
                }
            };

            PathDestinationData destinationData = new PathDestinationData()
            {
                DestinationType = request.Type,
                TargetAgentIndex = request.TargetAgentIndex,
                Destination = request.Destination,
                DesiredDestination = request.DesiredDestination,
                Offset = request.Offset,
            };

            PathLocationData locationData = new PathLocationData()
            {
                SectorToPicked = preallocations.SectorToPicked,
                DynamicAreaPickedSectorFlowStarts = preallocations.DynamicAreaSectorFlowStartList,
            };

            PathFlowData flowData = new PathFlowData()
            {
                DynamicAreaFlowField = preallocations.DynamicAreaFlowField,
            };

            PathPortalTraversalData portalTraversalData = new PathPortalTraversalData()
            {
                PortalSequenceBorders = preallocations.PortalSequenceBorders,
                PortalSequence = preallocations.PortalSequence,
                PortalTraversalDataArray = preallocations.PortalTraversalDataArray,
                SourcePortalIndexList = preallocations.SourcePortalIndexList,
                AStartTraverseIndexList = preallocations.AStartTraverseIndexList,
                TargetSectorPortalIndexList = preallocations.TargetSectorPortalIndexList,
                NewPickedSectorStartIndex = preallocations.NewPickedSectorStartIndex,
                PathAdditionSequenceBorderStartIndex = preallocations.PathAdditionSequenceBorderStartIndex,
                DiskstraStartIndicies = preallocations.DijkstraStartIndicies,
            };

            NativeArray<OverlappingDirection> sectorOverlappingDirections = new NativeArray<OverlappingDirection>(FlowFieldUtilities.SectorMatrixTileAmount, Allocator.Persistent);
            if (PathfindingInternalDataList.Count == pathIndex)
            {
                PathfindingInternalDataList.Add(internalData);
                PathLocationDataList.Add(locationData);
                PathFlowDataList.Add(flowData);
                PathSectorStateTableList.Add(preallocations.SectorStateTable);
                PathPortalTraversalDataList.Add(portalTraversalData);
                PathDestinationDataList.Add(destinationData);
                TargetSectorIntegrationList.Add(preallocations.TargetSectorCosts);
                PathRoutineDataList.Add(new PathRoutineData());
                PathSectorBitArrays.Add(preallocations.SectorBitArray);
                PathSubscriberCounts.Add(request.SourceCount);
                PathFlockIndicies.Add(request.FlockIndex);
                SectorOverlappingDirectionTableList.Add(sectorOverlappingDirections);
            }
            else
            {
                PathfindingInternalDataList[pathIndex] = internalData;
                PathLocationDataList[pathIndex] = locationData;
                PathFlowDataList[pathIndex] = flowData;
                PathSectorStateTableList[pathIndex] = preallocations.SectorStateTable;
                PathPortalTraversalDataList[pathIndex] = portalTraversalData;
                PathDestinationDataList[pathIndex] = destinationData;
                TargetSectorIntegrationList[pathIndex] = preallocations.TargetSectorCosts;
                PathRoutineDataList[pathIndex] = new PathRoutineData();
                PathSectorBitArrays[pathIndex] = preallocations.SectorBitArray;
                PathSubscriberCounts[pathIndex] = request.SourceCount;
                PathFlockIndicies[pathIndex] = request.FlockIndex;
                SectorOverlappingDirectionTableList[pathIndex] = sectorOverlappingDirections;
            }

            return pathIndex;
        }
        internal void FinalizePathBuffers(int pathIndex)
        {
            PathfindingInternalData internalData = PathfindingInternalDataList[pathIndex];
            PathFlowData pathFlowData = PathFlowDataList[pathIndex];
            pathFlowData.FlowField = _preallocator.GetFlowField(internalData.FlowFieldLength.Value);
            pathFlowData.LOSMap = new UnsafeLOSBitmap(internalData.FlowFieldLength.Value, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            internalData.IntegrationField = _preallocator.GetIntegrationField(internalData.FlowFieldLength.Value);
            internalData.ActivePortalList = _preallocator.GetActiveWaveFrontListPersistent(internalData.PickedSectorList.Length);
            PathfindingInternalDataList[pathIndex] = internalData;
            PathFlowDataList[pathIndex] = pathFlowData;
        }
        internal void ResizeActiveWaveFrontList(int newLength, NativeList<UnsafeList<ActiveWaveFront>> activeWaveFrontList)
        {
            _preallocator.AddToActiveWaveFrontList(newLength, activeWaveFrontList);
        }
        internal bool IsLOSCalculated(int pathIndex)
        {
            PathfindingInternalData internalData = PathfindingInternalDataList[pathIndex];
            PathLocationData locationData = PathLocationDataList[pathIndex];
            PathDestinationData destinationData = PathDestinationDataList[pathIndex];
            int sectorColAmount = FlowFieldUtilities.SectorColAmount;
            int sectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount;
            LocalIndex1d local = FlowFieldUtilities.GetLocal1D(FlowFieldUtilities.PosTo2D(destinationData.Destination, FlowFieldUtilities.TileSize, FlowFieldUtilities.FieldGridStartPosition), sectorColAmount, sectorMatrixColAmount);
            return (internalData.IntegrationField[locationData.SectorToPicked[local.sector] + local.index].Mark & IntegrationMark.LOSPass) == IntegrationMark.LOSPass;
        }
    }

}
