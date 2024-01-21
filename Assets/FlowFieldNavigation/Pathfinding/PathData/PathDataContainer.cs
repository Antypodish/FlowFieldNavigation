using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

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
    Stack<int> _removedPathIndicies;

    FieldDataContainer _fieldProducer;
    PathPreallocator _preallocator;
    internal PathDataContainer(PathfindingManager pathfindingManager)
    {
        _fieldProducer = pathfindingManager.FieldManager;
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
    }
    internal void Update()
    {
        for (int i = 0; i < PathfindingInternalDataList.Count; i++)
        {
            PathfindingInternalData internalData = PathfindingInternalDataList[i];
            PathState pathState = ExposedPathStateList[i];
            if(pathState == PathState.Removed) { continue; }
            int subsciriber = PathSubscriberCounts[i];
            if (subsciriber == 0)
            {
                PathLocationData locationData = PathLocationDataList[i];
                PathFlowData flowData = PathFlowDataList[i];
                UnsafeList<PathSectorState> sectorStateTable = PathSectorStateTableList[i];
                PathPortalTraversalData portalTraversalData = PathPortalTraversalDataList[i];
                UnsafeList<DijkstraTile> targetSectorIntegration = TargetSectorIntegrationList[i];
                PathDestinationData destinationData = PathDestinationDataList[i];
                SectorBitArray sectorBitArray = PathSectorBitArrays[i];
                sectorBitArray.Dispose();
                internalData.Dispose();
                locationData.Dispose();
                flowData.Dispose();
                portalTraversalData.Dispose();
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
                    FlowFieldLength = internalData.FlowFieldLength,
                    SourcePortalIndexList = portalTraversalData.SourcePortalIndexList,
                    AStartTraverseIndexList = portalTraversalData.AStartTraverseIndexList,
                    TargetSectorPortalIndexList = portalTraversalData.TargetSectorPortalIndexList,
                    PortalTraversalFastMarchingQueue = internalData.PortalTraversalQueue,
                    SectorStateTable = sectorStateTable,
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
            PathDestinationDataArray = PathDestinationDataList,
            PathFlowDataArray = PathFlowDataList,
            PathLocationDataArray = PathLocationDataList,
            PathFlockIndicies = PathFlockIndicies,
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
            NotActivePortalList = new NativeList<int>(Allocator.Persistent),
            SectorFlowStartIndiciesToCalculateIntegration = new NativeList<int>(Allocator.Persistent),
            SectorFlowStartIndiciesToCalculateFlow = new NativeList<int>(Allocator.Persistent),
            SectorWithinLOSState = new NativeArray<SectorsWihinLOSArgument>(1, Allocator.Persistent),
            DynamicArea = new DynamicArea()
            {
                FlowFieldCalculationBuffer = new UnsafeList<FlowData>(0, Allocator.Persistent),
                IntegrationField = new NativeList<IntegrationTile>(0, Allocator.Persistent),
                SectorFlowStartCalculationBuffer = new UnsafeList<SectorFlowStart>(0, Allocator.Persistent),
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
            DynamicAreaPickedSectorFlowStarts = new UnsafeList<SectorFlowStart>(0, Allocator.Persistent),
        };

        PathFlowData flowData = new PathFlowData()
        {
            DynamicAreaFlowField = new UnsafeList<FlowData>(0, Allocator.Persistent),
        };

        PathPortalTraversalData portalTraversalData = new PathPortalTraversalData()
        {
            PortalSequenceBorders = preallocations.PortalSequenceBorders,
            PortalSequence = preallocations.PortalSequence,
            PortalTraversalDataArray = preallocations.PortalTraversalDataArray,
            SourcePortalIndexList = preallocations.SourcePortalIndexList,
            AStartTraverseIndexList = preallocations.AStartTraverseIndexList,
            TargetSectorPortalIndexList = preallocations.TargetSectorPortalIndexList,
            NewPickedSectorStartIndex = new NativeArray<int>(1, Allocator.Persistent),
            PathAdditionSequenceBorderStartIndex = new NativeArray<int>(1, Allocator.Persistent),
        };
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
            PathSectorBitArrays.Add(new SectorBitArray(FlowFieldUtilities.SectorMatrixTileAmount, Allocator.Persistent));
            PathSubscriberCounts.Add(request.SourceCount);
            PathFlockIndicies.Add(request.FlockIndex);
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
            PathSectorBitArrays[pathIndex] = new SectorBitArray(FlowFieldUtilities.SectorMatrixTileAmount, Allocator.Persistent);
            PathSubscriberCounts[pathIndex] = request.SourceCount;
            PathFlockIndicies[pathIndex] = request.FlockIndex;
        }

        return pathIndex;
    }
    internal void FinalizePathBuffers(int pathIndex)
    {
        PathfindingInternalData internalData = PathfindingInternalDataList[pathIndex];
        PathFlowData pathFlowData = PathFlowDataList[pathIndex];
        NativeArray<int> flowFieldLength = internalData.FlowFieldLength;
        pathFlowData.FlowField = _preallocator.GetFlowField(flowFieldLength[0]);
        pathFlowData.LOSMap = new UnsafeLOSBitmap(flowFieldLength[0], Allocator.Persistent, NativeArrayOptions.ClearMemory);
        internalData.IntegrationField = _preallocator.GetIntegrationField(flowFieldLength[0]);
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
        LocalIndex1d local = FlowFieldUtilities.GetLocal1D(FlowFieldUtilities.PosTo2D(destinationData.Destination, FlowFieldUtilities.TileSize), sectorColAmount, sectorMatrixColAmount);
        return (internalData.IntegrationField[locationData.SectorToPicked[local.sector] + local.index].Mark & IntegrationMark.LOSPass) == IntegrationMark.LOSPass;
    }
}