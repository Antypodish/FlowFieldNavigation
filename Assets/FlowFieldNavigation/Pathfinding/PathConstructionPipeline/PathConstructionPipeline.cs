﻿using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using System.Collections.Generic;
using System.Diagnostics;
internal class PathConstructionPipeline
{
    const int _FinalPathRequestExpansionJobCount = 12;

    PathfindingManager _pathfindingManager;
    PathDataContainer _pathContainer;
    PortalTraversalScheduler _portalTravesalScheduler;
    RequestedSectorCalculationScheduler _requestedSectorCalculationScheduler;
    AdditionPortalTraversalScheduler _additionPortalTraversalScheduler;
    LOSIntegrationScheduler _losIntegrationScheduler;
    DynamicAreaScheduler _dynamicAreaScheduler;

    NativeList<float2> _sourcePositions;
    NativeList<OffsetDerivedPathRequest> _offsetDerivedPathRequests;
    NativeList<FinalPathRequest> _finalPathRequests;
    NativeReference<int> _currentPathSourceCount;
    NativeReference<int> _pathRequestSourceCount;
    NativeList<PathTask> _agentPathTaskList;
    NativeArray<IslandFieldProcessor> _islandFieldProcessors;
    NativeList<int> _newPathIndicies;
    NativeList<int> _destinationUpdatedPathIndicies;
    NativeList<int> _expandedPathIndicies;
    List<JobHandle> _pathfindingTaskOrganizationHandle;
    internal PathConstructionPipeline(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
        _pathContainer = pathfindingManager.PathDataContainer;
        _losIntegrationScheduler = new LOSIntegrationScheduler(pathfindingManager);
        _requestedSectorCalculationScheduler = new RequestedSectorCalculationScheduler(pathfindingManager, _losIntegrationScheduler);
        _portalTravesalScheduler = new PortalTraversalScheduler(pathfindingManager, _requestedSectorCalculationScheduler);
        _additionPortalTraversalScheduler = new AdditionPortalTraversalScheduler(pathfindingManager, _requestedSectorCalculationScheduler);
        _dynamicAreaScheduler = new DynamicAreaScheduler(pathfindingManager);
        _sourcePositions = new NativeList<float2>(Allocator.Persistent);
        _pathfindingTaskOrganizationHandle = new List<JobHandle>(1);
        _offsetDerivedPathRequests = new NativeList<OffsetDerivedPathRequest>(Allocator.Persistent);
        _finalPathRequests = new NativeList<FinalPathRequest>(Allocator.Persistent);
        _currentPathSourceCount = new NativeReference<int>(Allocator.Persistent);
        _pathRequestSourceCount = new NativeReference<int>(Allocator.Persistent);
        _agentPathTaskList = new NativeList<PathTask>(Allocator.Persistent);
        _newPathIndicies = new NativeList<int>(Allocator.Persistent);
        _destinationUpdatedPathIndicies = new NativeList<int>(Allocator.Persistent);
        _expandedPathIndicies = new NativeList<int>(Allocator.Persistent);
    }
    internal void DisposeAll()
    {
        _sourcePositions.Dispose();
        _pathfindingTaskOrganizationHandle = null;
        _offsetDerivedPathRequests.Dispose();
        _finalPathRequests.Dispose();
        _currentPathSourceCount.Dispose();
        _pathRequestSourceCount.Dispose();
        _agentPathTaskList.Dispose();
        _newPathIndicies.Dispose();
        _destinationUpdatedPathIndicies.Dispose();
        _expandedPathIndicies.Dispose();
        _portalTravesalScheduler.DisposeAll();
        _requestedSectorCalculationScheduler.DisposeAll();
        _additionPortalTraversalScheduler.DisposeAll();
        _losIntegrationScheduler.DisposeAll();
        _dynamicAreaScheduler.DisposeAll();
        _portalTravesalScheduler = null;
        _requestedSectorCalculationScheduler = null;
        _additionPortalTraversalScheduler = null;
        _losIntegrationScheduler = null;
        _dynamicAreaScheduler = null;
    }
    internal void ShcedulePathRequestEvalutaion(NativeList<PathRequest> requestedPaths,
        NativeArray<UnsafeListReadOnly<byte>> costFieldCosts,
        NativeArray<SectorBitArray>.ReadOnly editedSectorBitArray,
        NativeArray<IslandFieldProcessor> islandFieldProcessors,
        JobHandle islandFieldHandleAsDependency)
    {
        //RESET CONTAINERS
        _sourcePositions.Clear();
        _offsetDerivedPathRequests.Clear();
        _finalPathRequests.Clear();
        _newPathIndicies.Clear();
        _destinationUpdatedPathIndicies.Clear();
        _expandedPathIndicies.Clear();
        _pathRequestSourceCount.Value = 0;
        _currentPathSourceCount.Value = 0;

        NativeArray<AgentData> agentDataArray = _pathfindingManager.AgentDataContainer.AgentDataList;
        NativeArray<int> AgentNewPathIndicies = _pathfindingManager.AgentDataContainer.AgentNewPathIndicies;
        NativeArray<int> AgentCurPathIndicies = _pathfindingManager.AgentDataContainer.AgentCurPathIndicies;
        NativeArray<int> AgentFlockIndexArray = _pathfindingManager.AgentDataContainer.AgentFlockIndicies;
        _islandFieldProcessors = islandFieldProcessors;
        NativeArray<UnsafeList<DijkstraTile>> targetSectorIntegrations = _pathContainer.TargetSectorIntegrationList;
        NativeArray<PathLocationData> pathLocationDataArray = _pathContainer.PathLocationDataList;
        NativeArray<PathFlowData> pathFlowDataArray = _pathContainer.PathFlowDataList;
        NativeArray<PathState> pathStateArray = _pathContainer.ExposedPathStateList;
        NativeArray<PathDestinationData> pathDestinationDataArray = _pathContainer.PathDestinationDataList;
        NativeArray<PathRoutineData> pathRoutineDataArray = _pathContainer.PathRoutineDataList;
        NativeArray<UnsafeList<PathSectorState>> pathSectorStateTables = _pathContainer.PathSectorStateTableList;
        NativeArray<SectorBitArray> pathSectorBitArrays = _pathContainer.PathSectorBitArrays;
        NativeArray<int> pathFlockIndexArray = _pathContainer.PathFlockIndicies;

        FlockIndexSubmissionJob flockSubmission = new FlockIndexSubmissionJob()
        {
            InitialPathRequestCount = requestedPaths.Length,
            AgentFlockIndexArray = AgentFlockIndexArray,
            AgentNewPathIndexArray = AgentNewPathIndicies,
            InitialPathRequests = requestedPaths,
            FlockList = _pathfindingManager.FlockDataContainer.FlockList,
            UnusedFlockIndexList = _pathfindingManager.FlockDataContainer.UnusedFlockIndexList,
        };
        flockSubmission.Schedule().Complete();

        DynamicPathRequestSelfTargetingFixJob selfTargetingFix = new DynamicPathRequestSelfTargetingFixJob()
        {
            AgentNewPathIndicies = AgentNewPathIndicies,
            InitialPathRequests = requestedPaths,
        };
        JobHandle selfTargetingFixHandle = selfTargetingFix.Schedule();

        _agentPathTaskList.Length = agentDataArray.Length;
        NativeArrayCleaningJob<PathTask> agentTaskCleaning = new NativeArrayCleaningJob<PathTask>()
        {
            Array = _agentPathTaskList,
        };
        JobHandle agentTaskCleaningHandle = agentTaskCleaning.Schedule();

        PathRoutineDataResetJob routineDataReset = new PathRoutineDataResetJob()
        {
            PathOrganizationDataArray = pathRoutineDataArray,
        };
        JobHandle routineDataResetHandle = routineDataReset.Schedule(islandFieldHandleAsDependency);

        JobHandle sectorEditCheckHandle = routineDataResetHandle;
        if(editedSectorBitArray.Length != 0)
        {
            PathSectorEditCheckJob sectorEditCheck = new PathSectorEditCheckJob()
            {
                PathStateArray = pathStateArray,
                FieldEditSectorIDArray = editedSectorBitArray,
                PathSectorIDArray = pathSectorBitArrays,
                PathDestinationDataArray = pathDestinationDataArray,
                PathRoutineDataArray = pathRoutineDataArray,
            };
            sectorEditCheckHandle = sectorEditCheck.Schedule(pathRoutineDataArray.Length, 64, routineDataResetHandle);
        }

        PathRoutineDataCalculationJob routineDataCalculation = new PathRoutineDataCalculationJob()
        {
            SectorColAmount = FlowFieldUtilities.SectorColAmount,
            SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
            TileSize = FlowFieldUtilities.TileSize,
            SectorRowAmount = FlowFieldUtilities.SectorRowAmount,
            SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
            FieldColAmount = FlowFieldUtilities.FieldColAmount,
            FieldRowAmount = FlowFieldUtilities.FieldRowAmount,
            PathSectorStateTables = pathSectorStateTables,
            PathStateArray = pathStateArray,
            TargetSectorIntegrations = targetSectorIntegrations,
            AgentDataArray = agentDataArray,
            PathDestinationDataArray = pathDestinationDataArray,
            PathFlowDataArray = pathFlowDataArray,
            PathLocationDataArray = pathLocationDataArray,
            PathOrganizationDataArray = pathRoutineDataArray,
            IslandFieldProcessors = _islandFieldProcessors,
            CostFields = costFieldCosts,
        };
        JobHandle routineDataCalculationHandle = routineDataCalculation.Schedule(pathRoutineDataArray.Length, 64, sectorEditCheckHandle);

        CurrentPathReconstructionDeterminationJob reconstructionDetermination = new CurrentPathReconstructionDeterminationJob()
        {
            PathFlockIndexArray = pathFlockIndexArray,
            AgentCurPathIndicies = AgentCurPathIndicies,
            AgentNewPathIndicies = AgentNewPathIndicies,
            AgentDataArray = agentDataArray,
            PathRequests = requestedPaths,
            PathStateArray = pathStateArray,
            PathDestinationDataArray = pathDestinationDataArray,
            PathRoutineDataArray = pathRoutineDataArray,
        };
        JobHandle reconstructionDeterminationHandle = reconstructionDetermination.Schedule(JobHandle.CombineDependencies(routineDataCalculationHandle, agentTaskCleaningHandle, selfTargetingFixHandle));

        CurrentPathUpdateDeterminationJob updateDetermination = new CurrentPathUpdateDeterminationJob()
        {
            SectorColAmount = FlowFieldUtilities.SectorColAmount,
            SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
            TileSize = FlowFieldUtilities.TileSize,
            CurrentPathSourceCount = _currentPathSourceCount,
            AgentCurrentPathIndicies = AgentCurPathIndicies,
            AgentDataArray = agentDataArray,
            AgentNewPathIndicies = AgentNewPathIndicies,
            AgentPathTasks = _agentPathTaskList,
            PathSectorStateTableArray = pathSectorStateTables,
            PathFlowDataArray = pathFlowDataArray,
            PathLocationDataArray = pathLocationDataArray,
            PathRoutineDataArray = pathRoutineDataArray,
        };
        JobHandle updateDeterminationHandle = updateDetermination.Schedule(reconstructionDeterminationHandle);

        PathRequestOffsetDerivationJob offsetDerivation = new PathRequestOffsetDerivationJob()
        {
            TileSize = FlowFieldUtilities.TileSize,
            AgentDataArray = agentDataArray,
            InitialPathRequests = requestedPaths,
            DerivedPathRequests = _offsetDerivedPathRequests,
            NewAgentPathIndicies = AgentNewPathIndicies,
        };
        JobHandle offsetDerivationHandle = offsetDerivation.Schedule(updateDeterminationHandle);

        PathRequestIslandDerivationJob islandDerivation = new PathRequestIslandDerivationJob()
        {
            TileSize = FlowFieldUtilities.TileSize,
            AgentDataArray = agentDataArray,
            DerivedPathRequests = _offsetDerivedPathRequests,
            FinalPathRequests = _finalPathRequests,
            IslandFieldProcesorsPerOffset = _islandFieldProcessors,
            NewAgentPathIndicies = AgentNewPathIndicies,
            PathRequestSourceCount = _pathRequestSourceCount,
        };
        JobHandle islandDerivationHandle = islandDerivation.Schedule(offsetDerivationHandle);

        //Schdeule final path request destination expansions
        NativeArray<JobHandle> handles = new NativeArray<JobHandle>(_FinalPathRequestExpansionJobCount, Allocator.Temp);
        for(int i = 0; i < _FinalPathRequestExpansionJobCount; i++)
        {
            FinalPathRequestDestinationExpansionJob destinationExpansion = new FinalPathRequestDestinationExpansionJob()
            {
                JobIndex = i,
                TotalJobCount = _FinalPathRequestExpansionJobCount,
                SectorColAmount = FlowFieldUtilities.SectorColAmount,
                SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
                SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
                SectorRowAmount = FlowFieldUtilities.SectorRowAmount,
                FieldColAmount = FlowFieldUtilities.FieldColAmount,
                FieldRowAmount = FlowFieldUtilities.FieldRowAmount,
                TileSize = FlowFieldUtilities.TileSize,
                FieldMinXIncluding = FlowFieldUtilities.FieldMinXIncluding,
                FieldMinYIncluding = FlowFieldUtilities.FieldMinYIncluding,
                FieldMaxXExcluding = FlowFieldUtilities.FieldMaxXExcluding,
                FieldMaxYExcluding = FlowFieldUtilities.FieldMaxYExcluding,
                FinalPathRequests = _finalPathRequests,
                IslandFieldProcessors = _islandFieldProcessors,
                CostFields = costFieldCosts,
            };
            handles[i] = destinationExpansion.Schedule(islandDerivationHandle);
        }
        JobHandle combinedExpansionHanlde = JobHandle.CombineDependencies(handles);

        FinalPathRequestSourceSubmitJob sourceSubmit = new FinalPathRequestSourceSubmitJob()
        {
            Sources = _sourcePositions,
            AgentNewPathIndicies = AgentNewPathIndicies,
            AgentCurPathIndicies = AgentCurPathIndicies,
            AgentDataArray = agentDataArray,
            FinalPathRequests = _finalPathRequests,
            PathRequestSourceCount = _pathRequestSourceCount,
            CurrentPathSourceCount = _currentPathSourceCount,
            AgentTasks = _agentPathTaskList,
            PathStateArray = pathStateArray,
            PathRoutineDataArray = pathRoutineDataArray,
        };
        JobHandle sourceSubmitHandle = sourceSubmit.Schedule(JobHandle.CombineDependencies(combinedExpansionHanlde, islandDerivationHandle));

        if (FlowFieldUtilities.DebugMode) { sourceSubmitHandle.Complete(); }

        _pathfindingTaskOrganizationHandle.Add(sourceSubmitHandle);
    }
    void CompletePathEvaluation()
    {
        _pathfindingTaskOrganizationHandle[0].Complete();
        _pathfindingTaskOrganizationHandle.Clear();

        if (_islandFieldProcessors.IsCreated)
        {
            _islandFieldProcessors.Dispose();
        }

        //SET PATH INDICIES OF REQUESTED PATHS
        for (int i = 0; i < _finalPathRequests.Length; i++)
        {
            FinalPathRequest currentpath = _finalPathRequests[i];
            if (!currentpath.IsValid()) { continue; }
            NativeSlice<float2> pathSources = new NativeSlice<float2>(_sourcePositions, currentpath.SourcePositionStartIndex, currentpath.SourceCount);
            int newPathIndex = _pathContainer.CreatePath(currentpath);
            RequestPipelineInfoWithHandle requestInfo = new RequestPipelineInfoWithHandle(new JobHandle(), newPathIndex, i);
            _portalTravesalScheduler.SchedulePortalTraversalFor(requestInfo, pathSources);
            _newPathIndicies.Add(newPathIndex);
            currentpath.PathIndex = newPathIndex;
            _finalPathRequests[i] = currentpath;
        }

        //SCHEDULE PATH ADDITIONS AND FLOW REQUESTS
        NativeArray<PathRoutineData> pathRoutineDataArray = _pathContainer.PathRoutineDataList;
        for (int i = 0; i < pathRoutineDataArray.Length; i++)
        {
            PathRoutineData curPathRoutineData = pathRoutineDataArray[i];
            if (curPathRoutineData.Task == 0 && curPathRoutineData.DestinationState != DynamicDestinationState.Moved) { continue; }
            NativeSlice<float2> flowRequestSources = new NativeSlice<float2>(_sourcePositions, curPathRoutineData.FlowRequestSourceStart, curPathRoutineData.FlowRequestSourceCount);
            NativeSlice<float2> pathAdditionSources = new NativeSlice<float2>(_sourcePositions, curPathRoutineData.PathAdditionSourceStart, curPathRoutineData.PathAdditionSourceCount);
            PathPipelineInfoWithHandle pathInfo = new PathPipelineInfoWithHandle(new JobHandle(), i, curPathRoutineData.DestinationState);
            bool pathAdditionRequested = (curPathRoutineData.Task & PathTask.PathAdditionRequest) == PathTask.PathAdditionRequest;
            bool flowRequested = (curPathRoutineData.Task & PathTask.FlowRequest) == PathTask.FlowRequest;
            bool destinationMoved = curPathRoutineData.DestinationState == DynamicDestinationState.Moved;
            if (pathAdditionRequested)
            {
                _additionPortalTraversalScheduler.SchedulePortalTraversalFor(pathInfo, pathAdditionSources);
                _expandedPathIndicies.Add(pathInfo.PathIndex);
            }
            else if (flowRequested)
            {
                _requestedSectorCalculationScheduler.ScheduleRequestedSectorCalculation(pathInfo, new JobHandle(), flowRequestSources);
            }
            else if (destinationMoved)
            {
                _dynamicAreaScheduler.ScheduleDynamicArea(pathInfo);
                _losIntegrationScheduler.ScheduleLOS(pathInfo);
                _destinationUpdatedPathIndicies.Add(pathInfo.PathIndex);
            }
        }

        TryComplete();
    }

    internal void TryComplete()
    {
        if (_pathfindingTaskOrganizationHandle.Count != 0)
        {
            if (_pathfindingTaskOrganizationHandle[0].IsCompleted)
            {
                CompletePathEvaluation();
                _pathfindingTaskOrganizationHandle.Clear();
            }
        }
        if (_pathfindingTaskOrganizationHandle.Count == 0)
        {
            _portalTravesalScheduler.TryComplete(_finalPathRequests, _sourcePositions);
            _additionPortalTraversalScheduler.TryComplete(_sourcePositions);
            _requestedSectorCalculationScheduler.TryComplete();
        }
    }
    internal void ForceComplete()
    {
        if (_pathfindingTaskOrganizationHandle.Count != 0)
        {
            CompletePathEvaluation();
            _pathfindingTaskOrganizationHandle.Clear();
        }
        _dynamicAreaScheduler.ForceComplete();
        _portalTravesalScheduler.ForceComplete(_finalPathRequests, _sourcePositions);
        _additionPortalTraversalScheduler.ForceComplete(_sourcePositions);
        _requestedSectorCalculationScheduler.ForceComplete();
        _pathContainer.ExposeBuffers(_destinationUpdatedPathIndicies, _newPathIndicies, _expandedPathIndicies);
    }
    internal void TransferNewPathsToCurPaths()
    {
        //TRANSFER NEW PATH INDICIES TO CUR PATH INDICIES
        NewPathToCurPathTransferJob newPathToCurPathTransferJob = new NewPathToCurPathTransferJob()
        {
            AgentDestinationReachedArray = _pathfindingManager.AgentDataContainer.AgentDestinationReachedArray,
            AgentDataArray = _pathfindingManager.AgentDataContainer.AgentDataList,
            AgentCurPathIndicies = _pathfindingManager.AgentDataContainer.AgentCurPathIndicies,
            AgentNewPathIndicies = _pathfindingManager.AgentDataContainer.AgentNewPathIndicies,
            PathSubscribers = _pathfindingManager.PathDataContainer.PathSubscriberCounts,
            FinalPathRequests = _finalPathRequests,
        };
        newPathToCurPathTransferJob.Schedule().Complete();
    }
}
