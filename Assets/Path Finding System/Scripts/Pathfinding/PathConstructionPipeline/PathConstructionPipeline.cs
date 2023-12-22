using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using System.Collections.Generic;
using System.Diagnostics;
public class PathConstructionPipeline
{
    const int _FinalPathRequestExpansionJobCount = 12;

    PathfindingManager _pathfindingManager;
    PathContainer _pathContainer;
    PortalTraversalScheduler _portalTravesalScheduler;
    RequestedSectorCalculationScheduler _requestedSectorCalculationScheduler;
    AdditionPortalTraversalScheduler _additionPortalTraversalScheduler;
    LOSIntegrationScheduler _losIntegrationScheduler;
    DynamicAreaScheduler _dynamicAreaScheduler;

    NativeList<float2> SourcePositions;
    NativeList<OffsetDerivedPathRequest> OffsetDerivedPathRequests;
    NativeList<FinalPathRequest> FinalPathRequests;
    NativeReference<int> CurrentPathSourceCount;
    NativeReference<int> PathRequestSourceCount;
    NativeList<PathTask> AgentPathTaskList;
    NativeArray<IslandFieldProcessor> IslandFieldProcessors;
    List<JobHandle> _pathfindingTaskOrganizationHandle;
    public PathConstructionPipeline(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
        _pathContainer = pathfindingManager.PathContainer;
        _losIntegrationScheduler = new LOSIntegrationScheduler(pathfindingManager);
        _requestedSectorCalculationScheduler = new RequestedSectorCalculationScheduler(pathfindingManager, _losIntegrationScheduler);
        _portalTravesalScheduler = new PortalTraversalScheduler(pathfindingManager, _requestedSectorCalculationScheduler);
        _additionPortalTraversalScheduler = new AdditionPortalTraversalScheduler(pathfindingManager, _requestedSectorCalculationScheduler);
        _dynamicAreaScheduler = new DynamicAreaScheduler(pathfindingManager);
        SourcePositions = new NativeList<float2>(Allocator.Persistent);
        _pathfindingTaskOrganizationHandle = new List<JobHandle>(1);
        OffsetDerivedPathRequests = new NativeList<OffsetDerivedPathRequest>(Allocator.Persistent);
        FinalPathRequests = new NativeList<FinalPathRequest>(Allocator.Persistent);
        CurrentPathSourceCount = new NativeReference<int>(Allocator.Persistent);
        PathRequestSourceCount = new NativeReference<int>(Allocator.Persistent);
        AgentPathTaskList = new NativeList<PathTask>(Allocator.Persistent);
    }

    public void ShcedulePathRequestEvalutaion(NativeList<PathRequest> requestedPaths, NativeArray<UnsafeListReadOnly<byte>> costFieldCosts, NativeArray<SectorBitArray>.ReadOnly editedSectorBitArray, JobHandle islandFieldHandleAsDependency)
    {
        //RESET CONTAINERS
        SourcePositions.Clear();
        OffsetDerivedPathRequests.Clear();
        FinalPathRequests.Clear();
        PathRequestSourceCount.Value = 0;
        CurrentPathSourceCount.Value = 0;
        
        
        NativeArray<AgentData> agentDataArray = _pathfindingManager.AgentDataContainer.AgentDataList;
        NativeArray<int> AgentNewPathIndicies = _pathfindingManager.AgentDataContainer.AgentNewPathIndicies;
        NativeArray<int> AgentCurPathIndicies = _pathfindingManager.AgentDataContainer.AgentCurPathIndicies;
        IslandFieldProcessors = _pathfindingManager.FieldProducer.GetAllIslandFieldProcessors();
        NativeArray<UnsafeList<DijkstraTile>> targetSectorIntegrations = _pathContainer.TargetSectorIntegrationList;
        NativeArray<PathLocationData> pathLocationDataArray = _pathContainer.PathLocationDataList;
        NativeArray<PathFlowData> pathFlowDataArray = _pathContainer.PathFlowDataList;
        NativeArray<PathState> pathStateArray = _pathContainer.PathStateList;
        NativeArray<PathDestinationData> pathDestinationDataArray = _pathContainer.PathDestinationDataList;
        NativeArray<PathRoutineData> pathRoutineDataArray = _pathContainer.PathRoutineDataList;
        NativeArray<UnsafeList<PathSectorState>> pathSectorStateTables = _pathContainer.PathSectorStateTableList;
        NativeArray<SectorBitArray> pathSectorBitArrays = _pathContainer.PathSectorBitArrays;


        AgentPathTaskList.Length = agentDataArray.Length;
        NativeArrayCleaningJob<PathTask> agentTaskCleaning = new NativeArrayCleaningJob<PathTask>()
        {
            Array = AgentPathTaskList,
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
            IslandFieldProcessors = IslandFieldProcessors,
            CostFields = costFieldCosts,
        };
        JobHandle routineDataCalculationHandle = routineDataCalculation.Schedule(pathRoutineDataArray.Length, 64, sectorEditCheckHandle);

        CurrentPathReconstructionDeterminationJob reconstructionDetermination = new CurrentPathReconstructionDeterminationJob()
        {
            AgentCurPathIndicies = AgentCurPathIndicies,
            AgentNewPathIndicies = AgentNewPathIndicies,
            AgentDataArray = agentDataArray,
            PathRequests = requestedPaths,
            PathStateArray = pathStateArray,
            PathDestinationDataArray = pathDestinationDataArray,
            PathRoutineDataArray = pathRoutineDataArray,
        };
        JobHandle reconstructionDeterminationHandle = reconstructionDetermination.Schedule(JobHandle.CombineDependencies(routineDataCalculationHandle, agentTaskCleaningHandle));

        CurrentPathUpdateDeterminationJob updateDetermination = new CurrentPathUpdateDeterminationJob()
        {
            SectorColAmount = FlowFieldUtilities.SectorColAmount,
            SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
            TileSize = FlowFieldUtilities.TileSize,
            CurrentPathSourceCount = CurrentPathSourceCount,
            AgentCurrentPathIndicies = AgentCurPathIndicies,
            AgentDataArray = agentDataArray,
            AgentNewPathIndicies = AgentNewPathIndicies,
            AgentPathTasks = AgentPathTaskList,
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
            DerivedPathRequests = OffsetDerivedPathRequests,
            NewAgentPathIndicies = AgentNewPathIndicies,
        };
        JobHandle offsetDerivationHandle = offsetDerivation.Schedule(updateDeterminationHandle);

        PathRequestIslandDerivationJob islandDerivation = new PathRequestIslandDerivationJob()
        {
            TileSize = FlowFieldUtilities.TileSize,
            AgentDataArray = agentDataArray,
            DerivedPathRequests = OffsetDerivedPathRequests,
            FinalPathRequests = FinalPathRequests,
            IslandFieldProcesorsPerOffset = IslandFieldProcessors,
            NewAgentPathIndicies = AgentNewPathIndicies,
            PathRequestSourceCount = PathRequestSourceCount,
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
                FinalPathRequests = FinalPathRequests,
                IslandFieldProcessors = IslandFieldProcessors,
                CostFields = costFieldCosts,
            };
            handles[i] = destinationExpansion.Schedule(islandDerivationHandle);
        }
        JobHandle combinedExpansionHanlde = JobHandle.CombineDependencies(handles);

        FinalPathRequestSourceSubmitJob sourceSubmit = new FinalPathRequestSourceSubmitJob()
        {
            Sources = SourcePositions,
            AgentNewPathIndicies = AgentNewPathIndicies,
            AgentCurPathIndicies = AgentCurPathIndicies,
            AgentDataArray = agentDataArray,
            FinalPathRequests = FinalPathRequests,
            PathRequestSourceCount = PathRequestSourceCount,
            CurrentPathSourceCount = CurrentPathSourceCount,
            AgentTasks = AgentPathTaskList,
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
        if (IslandFieldProcessors.IsCreated)
        {
            IslandFieldProcessors.Dispose();
        }
        NativeArray<int> newPathIndicies = _pathfindingManager.AgentDataContainer.AgentNewPathIndicies;

        //SET PATH INDICIES OF REQUESTED PATHS
        for (int i = 0; i < FinalPathRequests.Length; i++)
        {
            FinalPathRequest currentpath = FinalPathRequests[i];
            if (!currentpath.IsValid()) { continue; }
            NativeSlice<float2> pathSources = new NativeSlice<float2>(SourcePositions, currentpath.SourcePositionStartIndex, currentpath.SourceCount);
            int newPathIndex = _pathContainer.CreatePath(currentpath);
            RequestPipelineInfoWithHandle requestInfo = new RequestPipelineInfoWithHandle(new JobHandle(), newPathIndex, i);
            _portalTravesalScheduler.SchedulePortalTraversalFor(requestInfo, pathSources);
            currentpath.PathIndex = newPathIndex;
            FinalPathRequests[i] = currentpath;
        }

        //SET NEW PATH INDICIES OF AGENTS
        OrganizedAgentNewPathIndiciesSetJob newpathindiciesSetJob = new OrganizedAgentNewPathIndiciesSetJob()
        {
            AgentNewPathIndicies = newPathIndicies,
            RequestedPaths = FinalPathRequests,
        };
        JobHandle newPathIndiciesHandle = newpathindiciesSetJob.Schedule();

        //SCHEDULE PATH ADDITIONS AND FLOW REQUESTS
        NativeArray<PathRoutineData> pathRoutineDataArray = _pathContainer.PathRoutineDataList;
        for (int i = 0; i < pathRoutineDataArray.Length; i++)
        {
            PathRoutineData curPathRoutineData = pathRoutineDataArray[i];
            if (curPathRoutineData.Task == 0 && curPathRoutineData.DestinationState != DynamicDestinationState.Moved) { continue; }
            NativeSlice<float2> flowRequestSources = new NativeSlice<float2>(SourcePositions, curPathRoutineData.FlowRequestSourceStart, curPathRoutineData.FlowRequestSourceCount);
            NativeSlice<float2> pathAdditionSources = new NativeSlice<float2>(SourcePositions, curPathRoutineData.PathAdditionSourceStart, curPathRoutineData.PathAdditionSourceCount);
            PathPipelineInfoWithHandle pathInfo = new PathPipelineInfoWithHandle(new JobHandle(), i, curPathRoutineData.DestinationState);
            bool pathAdditionRequested = (curPathRoutineData.Task & PathTask.PathAdditionRequest) == PathTask.PathAdditionRequest;
            bool flowRequested = (curPathRoutineData.Task & PathTask.FlowRequest) == PathTask.FlowRequest;
            bool destinationMoved = curPathRoutineData.DestinationState == DynamicDestinationState.Moved;
            if (pathAdditionRequested)
            {
                _additionPortalTraversalScheduler.SchedulePortalTraversalFor(pathInfo, pathAdditionSources);
            }
            else if (flowRequested)
            {
                _requestedSectorCalculationScheduler.ScheduleRequestedSectorCalculation(pathInfo, new JobHandle(), flowRequestSources);
            }
            else if (destinationMoved)
            {
                _dynamicAreaScheduler.ScheduleDynamicArea(pathInfo);
                _losIntegrationScheduler.ScheduleLOS(pathInfo);
            }
        }

        newPathIndiciesHandle.Complete();

        TryComplete();
    }

    public void TryComplete()
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
            _portalTravesalScheduler.TryComplete(FinalPathRequests, SourcePositions);
            _additionPortalTraversalScheduler.TryComplete(SourcePositions);
            _requestedSectorCalculationScheduler.TryComplete();
        }
    }
    public void ForceComplete()
    {
        if (_pathfindingTaskOrganizationHandle.Count != 0)
        {
            CompletePathEvaluation();
            _pathfindingTaskOrganizationHandle.Clear();
        }
        _dynamicAreaScheduler.ForceComplete();
        _portalTravesalScheduler.ForceComplete(FinalPathRequests, SourcePositions);
        _additionPortalTraversalScheduler.ForceComplete(SourcePositions);
        _requestedSectorCalculationScheduler.ForceComplete();
    }
}
public struct RequestPipelineInfoWithHandle
{
    public JobHandle Handle;
    public int PathIndex;
    public int RequestIndex;
    public DynamicDestinationState DestinationState;

    public RequestPipelineInfoWithHandle(JobHandle handle, int pathIndex, int requestIndex, DynamicDestinationState destinationState = DynamicDestinationState.None)
    {
        Handle = handle;
        PathIndex = pathIndex;
        RequestIndex = requestIndex;
        DestinationState = destinationState;
    }
    public PathPipelineInfoWithHandle ToPathPipelineInfoWithHandle()
    {
        return new PathPipelineInfoWithHandle()
        {
            Handle = Handle,
            PathIndex = PathIndex,
            DestinationState = DestinationState,
        };
    }
}
public struct PathPipelineInfoWithHandle
{
    public JobHandle Handle;
    public int PathIndex;
    public DynamicDestinationState DestinationState;
    public PathPipelineInfoWithHandle(JobHandle handle, int pathIndex, DynamicDestinationState destinationState = DynamicDestinationState.None)
    {
        Handle = handle;
        PathIndex = pathIndex;
        DestinationState = destinationState;
    }
}