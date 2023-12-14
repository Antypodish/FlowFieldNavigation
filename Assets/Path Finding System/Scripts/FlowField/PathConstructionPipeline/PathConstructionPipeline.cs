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
    PathContainer _pathProducer;
    PortalTraversalScheduler _portalTravesalScheduler;
    RequestedSectorCalculationScheduler _requestedSectorCalculationScheduler;
    AdditionPortalTraversalScheduler _additionPortalTraversalScheduler;
    LOSIntegrationScheduler _losIntegrationScheduler;
    DynamicAreaScheduler _dynamicAreaScheduler;

    NativeList<PathData> ExistingPathData;
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
        _pathProducer = pathfindingManager.PathContainer;
        _losIntegrationScheduler = new LOSIntegrationScheduler(pathfindingManager);
        _requestedSectorCalculationScheduler = new RequestedSectorCalculationScheduler(pathfindingManager, _losIntegrationScheduler);
        _portalTravesalScheduler = new PortalTraversalScheduler(pathfindingManager, _requestedSectorCalculationScheduler);
        _additionPortalTraversalScheduler = new AdditionPortalTraversalScheduler(pathfindingManager, _requestedSectorCalculationScheduler);
        _dynamicAreaScheduler = new DynamicAreaScheduler(pathfindingManager);
        ExistingPathData = new NativeList<PathData>(Allocator.Persistent);
        SourcePositions = new NativeList<float2>(Allocator.Persistent);
        _pathfindingTaskOrganizationHandle = new List<JobHandle>(1);
        OffsetDerivedPathRequests = new NativeList<OffsetDerivedPathRequest>(Allocator.Persistent);
        FinalPathRequests = new NativeList<FinalPathRequest>(Allocator.Persistent);
        CurrentPathSourceCount = new NativeReference<int>(Allocator.Persistent);
        PathRequestSourceCount = new NativeReference<int>(Allocator.Persistent);
        AgentPathTaskList = new NativeList<PathTask>(Allocator.Persistent);
    }

    public void ShcedulePathRequestEvalutaion(NativeList<PathRequest> requestedPaths, NativeArray<UnsafeListReadOnly<byte>> costFieldCosts, JobHandle islandFieldHandleAsDependency)
    {
        //RESET CONTAINERS
        ExistingPathData.Clear();
        SourcePositions.Clear();
        OffsetDerivedPathRequests.Clear();
        FinalPathRequests.Clear();
        PathRequestSourceCount.Value = 0;
        CurrentPathSourceCount.Value = 0;
        
        
        NativeArray<AgentData> agentData = _pathfindingManager.AgentDataContainer.AgentDataList;
        NativeArray<int> newPathIndicies = _pathfindingManager.AgentDataContainer.AgentNewPathIndicies;
        NativeArray<int> curPathIndicies = _pathfindingManager.AgentDataContainer.AgentCurPathIndicies;
        IslandFieldProcessors = _pathfindingManager.FieldProducer.GetAllIslandFieldProcessors();
        _pathProducer.GetCurrentPathData(ExistingPathData, agentData.AsReadOnly());


        AgentPathTaskList.Length = agentData.Length;
        NativeArrayCleaningJob<PathTask> agentTaskCleaning = new NativeArrayCleaningJob<PathTask>()
        {
            Array = AgentPathTaskList,
        };
        JobHandle agentTaskCleaningHandle = agentTaskCleaning.Schedule();

        CurrentPathReconstructionDeterminationJob reconstructionDetermination = new CurrentPathReconstructionDeterminationJob()
        {
            AgentCurPathIndicies = curPathIndicies,
            AgentNewPathIndicies = newPathIndicies,
            AgentDataArray = agentData,
            CurrentPaths = ExistingPathData,
            PathRequests = requestedPaths,
        };
        JobHandle reconstructionDeterminationHandle = reconstructionDetermination.Schedule(islandFieldHandleAsDependency);

        PathRequestOffsetDerivationJob offsetDerivation = new PathRequestOffsetDerivationJob()
        {
            TileSize = FlowFieldUtilities.TileSize,
            AgentDataArray = agentData,
            InitialPathRequests = requestedPaths,
            DerivedPathRequests = OffsetDerivedPathRequests,
            NewAgentPathIndicies = newPathIndicies,
        };
        JobHandle offsetDerivationHandle = offsetDerivation.Schedule(reconstructionDeterminationHandle);

        PathRequestIslandDerivationJob islandDerivation = new PathRequestIslandDerivationJob()
        {
            TileSize = FlowFieldUtilities.TileSize,
            AgentDataArray = agentData,
            DerivedPathRequests = OffsetDerivedPathRequests,
            FinalPathRequests = FinalPathRequests,
            IslandFieldProcesorsPerOffset = IslandFieldProcessors,
            NewAgentPathIndicies = newPathIndicies,
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

        CurrentPathUpdateDeterminationJob updateDetermination = new CurrentPathUpdateDeterminationJob()
        {
            SectorColAmount = FlowFieldUtilities.SectorColAmount,
            SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
            TileSize = FlowFieldUtilities.TileSize,
            CurrentPathSourceCount = CurrentPathSourceCount,
            AgentCurrentPathIndicies = curPathIndicies,
            AgentDataArray = agentData,
            AgentNewPathIndicies = newPathIndicies,
            CurrentPaths = ExistingPathData,
            AgentPathTasks = AgentPathTaskList,
        };
        JobHandle updateDeterminationHandle = updateDetermination.Schedule(JobHandle.CombineDependencies(islandDerivationHandle, agentTaskCleaningHandle));
        
        FinalPathRequestSourceSubmitJob sourceSubmit = new FinalPathRequestSourceSubmitJob()
        {
            Sources = SourcePositions,
            AgentNewPathIndicies = newPathIndicies,
            AgentCurPathIndicies = curPathIndicies,
            AgentDataArray = agentData,
            FinalPathRequests = FinalPathRequests,
            CurrentPaths = ExistingPathData,
            PathRequestSourceCount = PathRequestSourceCount,
            CurrentPathSourceCount = CurrentPathSourceCount,
            AgentTasks = AgentPathTaskList,
        };

        JobHandle sourceSubmitHandle = sourceSubmit.Schedule(JobHandle.CombineDependencies(combinedExpansionHanlde, updateDeterminationHandle));
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
            int newPathIndex = _pathProducer.CreatePath(currentpath);
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
        for (int i = 0; i < ExistingPathData.Length; i++)
        {
            PathData existingPath = ExistingPathData[i];
            if (existingPath.Task == 0 && existingPath.DestinationState != DynamicDestinationState.Moved) { continue; }
            NativeSlice<float2> flowRequestSources = new NativeSlice<float2>(SourcePositions, existingPath.FlowRequestSourceStart, existingPath.FlowRequestSourceCount);
            NativeSlice<float2> pathAdditionSources = new NativeSlice<float2>(SourcePositions, existingPath.PathAdditionSourceStart, existingPath.PathAdditionSourceCount);
            PathPipelineInfoWithHandle pathInfo = new PathPipelineInfoWithHandle(new JobHandle(), i, existingPath.DestinationState);
            bool pathAdditionRequested = (existingPath.Task & PathTask.PathAdditionRequest) == PathTask.PathAdditionRequest;
            bool flowRequested = (existingPath.Task & PathTask.FlowRequest) == PathTask.FlowRequest;
            bool destinationMoved = existingPath.DestinationState == DynamicDestinationState.Moved;
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
            _additionPortalTraversalScheduler.TryComplete(ExistingPathData, SourcePositions);
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
        _additionPortalTraversalScheduler.ForceComplete(ExistingPathData, SourcePositions);
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