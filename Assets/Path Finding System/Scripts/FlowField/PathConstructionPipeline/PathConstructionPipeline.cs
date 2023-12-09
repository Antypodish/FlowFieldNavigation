using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

public class PathConstructionPipeline
{
    PathfindingManager _pathfindingManager;
    PathContainer _pathProducer;
    PortalTraversalScheduler _portalTravesalScheduler;
    RequestedSectorCalculationScheduler _requestedSectorCalculationScheduler;
    AdditionPortalTraversalScheduler _additionPortalTraversalScheduler;
    LOSIntegrationScheduler _losIntegrationScheduler;
    DynamicAreaScheduler _dynamicAreaScheduler;

    NativeList<PathData> ExistingPathData;
    NativeList<float2> SourcePositions;
    NativeList<PathRequest> RequestedPaths;
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
    }

    public void EvaluatePathRequests(NativeList<PathRequest> requestedPaths)
    {
        //RESET CONTAINERS
        ExistingPathData.Clear();
        SourcePositions.Clear();

        RequestedPaths = requestedPaths;
        NativeArray<AgentData> agentData = _pathfindingManager.AgentDataContainer.AgentDataList;
        NativeArray<int> newPathIndicies = _pathfindingManager.AgentDataContainer.AgentNewPathIndicies;
        NativeArray<int> curPathIndicies = _pathfindingManager.AgentDataContainer.AgentCurPathIndicies;
        NativeArray<IslandFieldProcessor> islandFieldPorcessor = _pathfindingManager.FieldProducer.GetAllIslandFieldProcessors();
        _pathProducer.GetCurrentPathData(ExistingPathData, agentData.AsReadOnly());

        PathfindingTaskOrganizationJob organization = new PathfindingTaskOrganizationJob()
        {
            TileSize = FlowFieldUtilities.TileSize,
            SectorColAmount = FlowFieldUtilities.SectorColAmount,
            SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
            AgentData = agentData,
            AgentNewPathIndicies = newPathIndicies,
            AgentCurrentPathIndicies = curPathIndicies,
            PathfindingSources = SourcePositions,
            IslandFieldProcessors = islandFieldPorcessor,
            NewPaths = requestedPaths,
            CurrentPaths = ExistingPathData,
            PathSubscribers = _pathfindingManager.PathContainer.ProducedPathSubscribers,
        };
        organization.Schedule().Complete();
        islandFieldPorcessor.Dispose();

        //SET PATH INDICIES OF REQUESTED PATHS
        for (int i = 0; i < requestedPaths.Length; i++)
        {
            PathRequest currentpath = requestedPaths[i];
            if (!currentpath.IsValid()) { continue; }
            NativeSlice<float2> pathSources = new NativeSlice<float2>(organization.PathfindingSources, currentpath.SourcePositionStartIndex, currentpath.AgentCount);
            int newPathIndex = _pathProducer.CreatePath(currentpath);
            RequestPipelineInfoWithHandle requestInfo = new RequestPipelineInfoWithHandle(new JobHandle(), newPathIndex, i);
            _portalTravesalScheduler.SchedulePortalTraversalFor(requestInfo, pathSources);
            currentpath.PathIndex = newPathIndex;
            requestedPaths[i] = currentpath;
        }

        //SET NEW PATH INDICIES OF AGENTS
        OrganizedAgentNewPathIndiciesSetJob newpathindiciesSetJob = new OrganizedAgentNewPathIndiciesSetJob()
        {
            AgentNewPathIndicies = newPathIndicies,
            RequestedPaths = requestedPaths,
        };
        JobHandle newPathIndiciesHandle = newpathindiciesSetJob.Schedule();
        
        //SCHEDULE PATH ADDITIONS AND FLOW REQUESTS
        for (int i = 0; i < ExistingPathData.Length; i++)
        {
            PathData existingPath = ExistingPathData[i];
            if (existingPath.Task == 0 && existingPath.DestinationState != DynamicDestinationState.Moved) { continue; }
            NativeSlice<float2> flowRequestSources = new NativeSlice<float2>(organization.PathfindingSources, existingPath.FlowRequestSourceStart, existingPath.FlowRequestSourceCount);
            NativeSlice<float2> pathAdditionSources = new NativeSlice<float2>(organization.PathfindingSources, existingPath.PathAdditionSourceStart, existingPath.PathAdditionSourceCount);
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
        _portalTravesalScheduler.TryComplete(RequestedPaths, SourcePositions);
        _additionPortalTraversalScheduler.TryComplete(ExistingPathData, SourcePositions);
        _requestedSectorCalculationScheduler.TryComplete();
    }
    public void ForceComplete()
    {
        _dynamicAreaScheduler.ForceComplete();
        _portalTravesalScheduler.ForceComplete(RequestedPaths, SourcePositions);
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