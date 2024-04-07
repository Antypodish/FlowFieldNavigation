﻿using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace FlowFieldNavigation
{
    internal class PathDataContainer
    {
        internal NativeList<FlowData> ExposedFlowData;
        internal NativeList<bool> ExposedLosData;
        internal PathSectorToFlowStartMapper SectorFlowStartMap;
        internal NativeList<float2> ExposedPathDestinations;
        internal NativeList<int> ExposedPathFlockIndicies;
        internal NativeList<float> ExposedPathReachDistanceCheckRanges;
        internal NativeList<PathState> ExposedPathStateList;
        internal NativeList<bool> ExposedPathAgentStopFlagList;
        internal List<NativeArray<int>> SectorToFlowStartTables;
        internal List<PathfindingInternalData> PathfindingInternalDataList;
        internal NativeList<UnsafeList<PathSectorState>> PathSectorStateTableList;
        internal NativeList<PathDestinationData> PathDestinationDataList;
        internal NativeList<UnsafeList<float>> TargetSectorIntegrationList;
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
            PathSectorStateTableList = new NativeList<UnsafeList<PathSectorState>>(Allocator.Persistent);
            PathPortalTraversalDataList = new List<PathPortalTraversalData>();
            PathDestinationDataList = new NativeList<PathDestinationData>(Allocator.Persistent);
            ExposedPathStateList = new NativeList<PathState>(Allocator.Persistent);
            TargetSectorIntegrationList = new NativeList<UnsafeList<float>>(Allocator.Persistent);
            PathRoutineDataList = new NativeList<PathRoutineData>(Allocator.Persistent);
            PathSectorBitArrays = new NativeList<SectorBitArray>(Allocator.Persistent);
            SectorToFlowStartTables = new List<NativeArray<int>>();

            ExposedPathDestinations = new NativeList<float2>(Allocator.Persistent);
            PathFlockIndicies = new NativeList<int>(Allocator.Persistent);
            ExposedPathFlockIndicies = new NativeList<int>(Allocator.Persistent);
            ExposedPathReachDistanceCheckRanges = new NativeList<float>(Allocator.Persistent);
            ExposedPathAgentStopFlagList = new NativeList<bool>(Allocator.Persistent);
            SectorOverlappingDirectionTableList = new List<NativeArray<OverlappingDirection>>();

            ExposedFlowData = new NativeList<FlowData>(Allocator.Persistent);
            SectorFlowStartMap = new PathSectorToFlowStartMapper(0, Allocator.Persistent);
            ExposedLosData = new NativeList<bool>(Allocator.Persistent);
        }
        internal void DisposeAll()
        {
            if (ExposedPathDestinations.IsCreated) { ExposedPathDestinations.Dispose(); }
            if (ExposedPathFlockIndicies.IsCreated) { ExposedPathFlockIndicies.Dispose(); }
            if (ExposedPathReachDistanceCheckRanges.IsCreated) { ExposedPathReachDistanceCheckRanges.Dispose(); }
            if (ExposedPathStateList.IsCreated) { ExposedPathStateList.Dispose(); }
            if (ExposedPathAgentStopFlagList.IsCreated) { ExposedPathAgentStopFlagList.Dispose(); }
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
                    UnsafeList<PathSectorState> sectorStateTable = PathSectorStateTableList[i];
                    PathPortalTraversalData portalTraversalData = PathPortalTraversalDataList[i];
                    UnsafeList<float> targetSectorIntegration = TargetSectorIntegrationList[i];
                    SectorBitArray sectorBitArray = PathSectorBitArrays[i];
                    NativeArray<OverlappingDirection> sectorOverlappingDirections = SectorOverlappingDirectionTableList[i];
                    NativeArray<int> sectorToFlowStartTable = SectorToFlowStartTables[i];
                    RemoveFromPathSectorToFlowStartMapper(internalData.PickedSectorList.AsArray(), i);
                    sectorOverlappingDirections.Dispose();
                    portalTraversalData.GoalDataList.Dispose();
                    internalData.SectorToWaveFrontsMap.Dispose();
                    internalData.IntegrationField.Dispose();
                    portalTraversalData.NewReducedPortalIndicies.Dispose();
                    portalTraversalData.PortalDataRecords.Dispose();
                    internalData.LOSCalculatedFlag.Dispose();
                    internalData.FlowFieldCalculationBuffer.Dispose();
                    sectorToFlowStartTable.Dispose();
                    ExposedPathStateList[i] = PathState.Removed;
                    _removedPathIndicies.Push(i);
                    PreallocationPack preallocations = new PreallocationPack()
                    {
                        PickedToSector = internalData.PickedSectorList,
                        PortalSequence = portalTraversalData.PortalSequence,
                        TargetSectorCosts = targetSectorIntegration,
                        SourcePortalIndexList = portalTraversalData.SourcePortalIndexList,
                        TargetSectorPortalIndexList = portalTraversalData.TargetSectorPortalIndexList,
                        PortalTraversalFastMarchingQueue = internalData.PortalTraversalQueue,
                        SectorStateTable = sectorStateTable,
                        SectorStartIndexListToCalculateFlow = internalData.SectorIndiciesToCalculateFlow,
                        SectorStartIndexListToCalculateIntegration = internalData.SectorIndiciesToCalculateIntegration,
                        NotActivePortalList = internalData.NotActivePortalList,
                        NewPickedSectorStartIndex = portalTraversalData.NewPickedSectorStartIndex,
                        PathAdditionSequenceBorderStartIndex = portalTraversalData.PathAdditionSequenceSliceStartIndex,
                        DynamicAreaIntegrationField = internalData.DynamicArea.IntegrationField,
                        DynamicAreaFlowFieldCalculationBuffer = internalData.DynamicArea.FlowFieldCalculationBuffer,
                        DynamicAreaSectorFlowStartCalculationList = internalData.DynamicArea.SectorFlowStartCalculationBuffer,
                        SectorsWithinLOSState = internalData.SectorWithinLOSState,
                        SectorBitArray = sectorBitArray,
                        DijkstraStartIndicies = portalTraversalData.DiskstraStartIndicies,
                    };
                    _preallocator.SendPreallocationsBack(ref preallocations);
                }
            }
            _preallocator.CheckForDeallocations();
        }
        void RemoveFromPathSectorToFlowStartMapper(NativeArray<int> pickedSectorList, int pathIndex)
        {
            for(int i = 0; i < pickedSectorList.Length; i++)
            {
                int sector = pickedSectorList[i];
                SectorFlowStartMap.TryRemove(pathIndex, sector);
            }
        }
        internal void ExposeBuffers(NativeArray<int> destinationUpdatedPathIndicies, NativeArray<int> newPathIndicies, NativeArray<int> expandedPathIndicies)
        {
            PathDataExposeJob dataExposeJob = new PathDataExposeJob()
            {
                DestinationUpdatedPathIndicies = destinationUpdatedPathIndicies,
                NewPathIndicies = newPathIndicies,
                ExpandedPathIndicies = expandedPathIndicies,
                ExposedPathDestinationList = ExposedPathDestinations,
                ExposedPathFlockIndicies = ExposedPathFlockIndicies,
                ExposedPathReachDistanceCheckRange = ExposedPathReachDistanceCheckRanges,
                PathStopFlagList = ExposedPathAgentStopFlagList,
                PathStateList = ExposedPathStateList,
                PathDestinationDataArray = PathDestinationDataList.AsArray(),
                PathFlockIndicies = PathFlockIndicies.AsArray(),
            };
            dataExposeJob.Schedule().Complete();
        }
        internal int CreatePath(FinalPathRequest request)
        {
            PreallocationPack preallocations = _preallocator.GetPreallocations();

            int pathIndex;
            if (_removedPathIndicies.Count != 0) { pathIndex = _removedPathIndicies.Pop(); }
            else { pathIndex = PathfindingInternalDataList.Count; }

            PathfindingInternalData internalData = new PathfindingInternalData()
            {
                PickedSectorList = preallocations.PickedToSector,
                PortalTraversalQueue = preallocations.PortalTraversalFastMarchingQueue,
                NotActivePortalList = preallocations.NotActivePortalList,
                SectorIndiciesToCalculateIntegration = preallocations.SectorStartIndexListToCalculateIntegration,
                SectorIndiciesToCalculateFlow = preallocations.SectorStartIndexListToCalculateFlow,
                SectorWithinLOSState = preallocations.SectorsWithinLOSState,
                SectorToWaveFrontsMap = new NativeParallelMultiHashMap<int, ActiveWaveFront>(0, Allocator.Persistent),
                IntegrationField = new NativeList<IntegrationTile>(Allocator.Persistent),
                LOSCalculatedFlag = new NativeReference<bool>(false, Allocator.Persistent),
                FlowFieldCalculationBuffer = new NativeList<FlowData>(Allocator.Persistent),
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
            PathPortalTraversalData portalTraversalData = new PathPortalTraversalData()
            {
                PortalSequenceSlices = new NativeList<Slice>(Allocator.Persistent),
                PortalSequence = preallocations.PortalSequence,
                SourcePortalIndexList = preallocations.SourcePortalIndexList,
                TargetSectorPortalIndexList = preallocations.TargetSectorPortalIndexList,
                NewPickedSectorStartIndex = preallocations.NewPickedSectorStartIndex,
                PathAdditionSequenceSliceStartIndex = preallocations.PathAdditionSequenceBorderStartIndex,
                DiskstraStartIndicies = preallocations.DijkstraStartIndicies,
                GoalDataList = new NativeList<PortalTraversalData>(Allocator.Persistent),
                NewReducedPortalIndicies = new NativeList<int>(Allocator.Persistent),
                PortalDataRecords = new NativeList<PortalTraversalDataRecord>(Allocator.Persistent),
            };

            NativeArray<OverlappingDirection> sectorOverlappingDirections = new NativeArray<OverlappingDirection>(FlowFieldUtilities.SectorMatrixTileAmount, Allocator.Persistent);
            if (PathfindingInternalDataList.Count == pathIndex)
            {
                PathfindingInternalDataList.Add(internalData);
                PathSectorStateTableList.Add(preallocations.SectorStateTable);
                PathPortalTraversalDataList.Add(portalTraversalData);
                PathDestinationDataList.Add(destinationData);
                TargetSectorIntegrationList.Add(preallocations.TargetSectorCosts);
                PathRoutineDataList.Add(new PathRoutineData());
                PathSectorBitArrays.Add(preallocations.SectorBitArray);
                PathSubscriberCounts.Add(request.SourceCount);
                PathFlockIndicies.Add(request.FlockIndex);
                SectorOverlappingDirectionTableList.Add(sectorOverlappingDirections);
                SectorToFlowStartTables.Add(new NativeArray<int>(FlowFieldUtilities.SectorMatrixTileAmount, Allocator.Persistent));
            }
            else
            {
                PathfindingInternalDataList[pathIndex] = internalData;
                PathSectorStateTableList[pathIndex] = preallocations.SectorStateTable;
                PathPortalTraversalDataList[pathIndex] = portalTraversalData;
                PathDestinationDataList[pathIndex] = destinationData;
                TargetSectorIntegrationList[pathIndex] = preallocations.TargetSectorCosts;
                PathRoutineDataList[pathIndex] = new PathRoutineData();
                PathSectorBitArrays[pathIndex] = preallocations.SectorBitArray;
                PathSubscriberCounts[pathIndex] = request.SourceCount;
                PathFlockIndicies[pathIndex] = request.FlockIndex;
                SectorOverlappingDirectionTableList[pathIndex] = sectorOverlappingDirections;
                SectorToFlowStartTables[pathIndex] = new NativeArray<int>(FlowFieldUtilities.SectorMatrixTileAmount, Allocator.Persistent);
            }

            return pathIndex;
        }
    }

}
