using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

public class PathConstructionPipeline
{
    PathfindingManager _pathfindingManager;
    PathProducer _pathProducer;
    PortalTraversalScheduler _portalTravesalScheduler;
    RequestedSectorCalculationScheduler _requestedSectorCalculationScheduler;
    AdditionPortalTraversalScheduler _additionPortalTraversalScheduler;

    NativeList<PathData> ExistingPathData;
    NativeList<float2> SourcePositions;
    NativeArray<PathRequest> RequestedPaths;
    public PathConstructionPipeline(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
        _pathProducer = pathfindingManager.PathProducer;
        _requestedSectorCalculationScheduler = new RequestedSectorCalculationScheduler(pathfindingManager);
        _portalTravesalScheduler = new PortalTraversalScheduler(pathfindingManager, _requestedSectorCalculationScheduler);
        _additionPortalTraversalScheduler = new AdditionPortalTraversalScheduler(pathfindingManager, _requestedSectorCalculationScheduler);
        ExistingPathData = new NativeList<PathData>(Allocator.Persistent);
        SourcePositions = new NativeList<float2>(Allocator.Persistent);
    }

    public void EvaluatePathRequests(NativeArray<PathRequest> requestedPaths)
    {
        //RESET CONTAINERS
        ExistingPathData.Clear();
        SourcePositions.Clear();

        RequestedPaths = requestedPaths;
        _pathProducer.GetCurrentPathData(ExistingPathData);
        NativeArray<AgentData> agentData = _pathfindingManager.AgentDataContainer.AgentDataList;
        NativeArray<int> newPathIndicies = _pathfindingManager.AgentDataContainer.AgentNewPathIndicies;
        NativeArray<int> curPathIndicies = _pathfindingManager.AgentDataContainer.AgentCurPathIndicies;
        NativeArray<IslandFieldProcessor> islandFieldPorcessor = _pathfindingManager.FieldProducer.GetAllIslandFieldProcessors();

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
            PathSubscribers = _pathfindingManager.PathProducer.ProducedPathSubscribers,
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
            _portalTravesalScheduler.SchedulePortalTraversalFor(newPathIndex, i, pathSources);
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
            if (existingPath.Task == 0) { continue; }
            NativeSlice<float2> flowRequestSources = new NativeSlice<float2>(organization.PathfindingSources, existingPath.FlowRequestSourceStart, existingPath.FlowRequestSourceCount);
            NativeSlice<float2> pathAdditionSources = new NativeSlice<float2>(organization.PathfindingSources, existingPath.PathAdditionSourceStart, existingPath.PathAdditionSourceCount);
            bool pathAdditionRequested = (existingPath.Task & PathTask.PathAdditionRequest) == PathTask.PathAdditionRequest;
            bool flowRequested = (existingPath.Task & PathTask.FlowRequest) == PathTask.FlowRequest;
            if (pathAdditionRequested)
            {
                _additionPortalTraversalScheduler.SchedulePortalTraversalFor(i, pathAdditionSources);
            }
            else if (flowRequested)
            {
                _requestedSectorCalculationScheduler.ScheduleRequestedSectorCalculation(i, new JobHandle(), flowRequestSources); 
            }
        }

        newPathIndiciesHandle.Complete();
    }

    public void TryComplete()
    {
        _portalTravesalScheduler.TryComplete(RequestedPaths, SourcePositions);
        _additionPortalTraversalScheduler.TryComplete(ExistingPathData, SourcePositions);
        _requestedSectorCalculationScheduler.TryComplete();
    }
    public void ForceComplete()
    {
        _portalTravesalScheduler.ForceComplete(RequestedPaths, SourcePositions);
        _additionPortalTraversalScheduler.ForceComplete(ExistingPathData, SourcePositions);
        _requestedSectorCalculationScheduler.ForceComplete();
    }
}
public struct HandleWithReqAndPathIndex
{
    public JobHandle Handle;
    public int PathIndex;
    public int RequestIndex;

    public HandleWithReqAndPathIndex(JobHandle handle, int pathIndex, int requestIndex)
    { 
        Handle = handle; 
        PathIndex = pathIndex; 
        RequestIndex = requestIndex; 
    }
}
public struct HandleWithPathIndex
{
    public JobHandle Handle;
    public int PathIndex;

    public HandleWithPathIndex(JobHandle handle, int pathIndex) 
    { 
        Handle = handle; 
        PathIndex = pathIndex; 
    }
}