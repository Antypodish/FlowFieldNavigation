using Assets.Path_Finding_System.Scripts;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Jobs;
using Unity.Collections.LowLevel.Unsafe;

public class RoutineScheduler
{
    PathfindingManager _pathfindingManager;
    AgentRoutineDataProducer _dirCalculator;

    List<NewPathHandle> _pathProdCalcHandles;

    List<JobHandle> _costEditHandle;
    List<JobHandle> _islandReconfigHandle;
    List<NewPathHandle> _porTravHandles;
    List<JobHandle> _agentMovementCalculationHandle;
    List<JobHandle> _flowRequestHandles;
    List<ExistingPathHandle> _additionPortalTravesalHandles;
    List<ExistingPathHandle> _additionPathConstructionHandles;

    public NativeList<PathRequest> CurrentRequestedPaths;
    public NativeList<float2> CurrentSourcePositions;
    public NativeArray<PathData> ExistingPaths;

    int _costFieldEditRequestCount = 0;
    public RoutineScheduler(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
        _dirCalculator = new AgentRoutineDataProducer(pathfindingManager.AgentDataContainer, pathfindingManager);
        _porTravHandles = new List<NewPathHandle>();
        _agentMovementCalculationHandle = new List<JobHandle>();
        _pathProdCalcHandles = new List<NewPathHandle>();
        _costEditHandle = new List<JobHandle>();
        CurrentRequestedPaths = new NativeList<PathRequest>(Allocator.Persistent);
        _islandReconfigHandle = new List<JobHandle>();
        CurrentSourcePositions = new NativeList<float2>(Allocator.Persistent);
        _flowRequestHandles = new List<JobHandle>();
        _additionPortalTravesalHandles = new List<ExistingPathHandle>();
        _additionPathConstructionHandles = new List<ExistingPathHandle>();
    }

    public void Schedule(List<CostFieldEditJob[]> costEditJobs, NativeList<PathRequest> newPaths)
    {
        _costFieldEditRequestCount = costEditJobs.Count;
        ExistingPaths = _pathfindingManager.PathProducer.GetCurrentPathData();

        //SCHEDULE COST EDITS
        JobHandle costEditHandle = ScheduleCostEditRequests(costEditJobs);
        JobHandle islandFieldReconfigHandle = ScheduleIslandFieldReconfig(costEditHandle);
        _costEditHandle.Add(costEditHandle);
        _islandReconfigHandle.Add(islandFieldReconfigHandle);

        //SET POSITIONS OF AGENT DATA
        NativeArray<AgentData> agentData = _pathfindingManager.AgentDataContainer.AgentDataList;
        TransformAccessArray agentTransforms = _pathfindingManager.AgentDataContainer.AgentTransforms;
        AgentDataSetPositionJob posSetJob = new AgentDataSetPositionJob()
        {
            AgentDataArray = agentData,
        };
        JobHandle posSetHandle = posSetJob.Schedule(agentTransforms);

        //COPY REQUESTED TO SCHEDULING SYSTEM
        NativeListCopyJob<PathRequest> copyJob = new NativeListCopyJob<PathRequest>()
        {
            Source = newPaths,
            Destination = CurrentRequestedPaths,
        };
        JobHandle copyHandle = copyJob.Schedule();

        //TRANSFER REQUESTED PATHS TO NEW PATHS
        RequestedToNewPathIndexTransferJob reqToNewTransfer = new RequestedToNewPathIndexTransferJob()
        {
            AgentRequestedPathIndicies = _pathfindingManager.AgentDataContainer.AgentRequestedPathIndicies,
            AgentNewPathIndicies = _pathfindingManager.AgentDataContainer.AgentNewPathIndicies,
        };
        JobHandle transferHandle = reqToNewTransfer.Schedule();

        JobHandle.CombineDependencies(transferHandle, copyHandle, posSetHandle).Complete();

        _dirCalculator.PrepareAgentMovementDataCalculationJob();

        ScheduleAgentMovementJobs(costEditHandle);
    }
    public void TryCompletePredecessorJobs()
    {
        //ISLAND REC
        if (_islandReconfigHandle.Count != 0)
        {
            if (_islandReconfigHandle[0].IsCompleted)
            {
                _islandReconfigHandle[0].Complete();
                _islandReconfigHandle.RemoveAtSwapBack(0);

                RunPathfindingDataOrganization();
            }
        }
        //POR TRAV
        if (_porTravHandles.Count != 0)
        {
            for (int i = 0; i < _porTravHandles.Count; i++)
            {
                NewPathHandle pathHandle = _porTravHandles[i];
                if (pathHandle.Handle.IsCompleted)
                {
                    pathHandle.Handle.Complete();
                    SchedulePathProduction(pathHandle.PathIndex, pathHandle.Soruces);
                    _porTravHandles.RemoveAtSwapBack(i);
                }
            }
        }
        //ADD POR TRAV
        if (_additionPortalTravesalHandles.Count != 0)
        {
            for (int i = 0; i < _additionPortalTravesalHandles.Count; i++)
            {
                ExistingPathHandle existingPathHandle = _additionPortalTravesalHandles[i];
                existingPathHandle.Handle.Complete();
                ScheduleAdditionPathConstructionWithFlowRequests(existingPathHandle.PathIndex);
                _additionPortalTravesalHandles.RemoveAtSwapBack(i);
            }
        }
    }
    public void ForceCompleteAll()
    {
        Stopwatch sw = new Stopwatch();
        sw.Start();
        //COST EDIT
        if (_costEditHandle.Count != 0)
        {
            _costEditHandle[0].Complete();
            _costEditHandle.RemoveAtSwapBack(0);
        }
        //ISLAND RECONFİG
        if (_islandReconfigHandle.Count != 0)
        {
            _islandReconfigHandle[0].Complete();
            _islandReconfigHandle.RemoveAtSwapBack(0);
            RunPathfindingDataOrganization();
        }
        //POR TRAV
        if (_porTravHandles.Count != 0)
        {
            for (int i = 0; i < _porTravHandles.Count; i++)
            {
                NewPathHandle pathHandle = _porTravHandles[i];
                pathHandle.Handle.Complete();
                SchedulePathProduction(pathHandle.PathIndex, pathHandle.Soruces);
            }
            _porTravHandles.Clear();
        }
        //PATH PROD
        if (_pathProdCalcHandles.Count != 0)
        {
            for (int i = 0; i < _pathProdCalcHandles.Count; i++)
            {
                NewPathHandle pathHandle = _pathProdCalcHandles[i];
                pathHandle.Handle.Complete();
                _pathfindingManager.PathProducer.ProducedPaths[pathHandle.PathIndex].IsCalculated = true;
            }
            _pathProdCalcHandles.Clear();
        }
        //FLOW REQ
        if(_flowRequestHandles.Count != 0)
        {
            for(int i = 0; i < _flowRequestHandles.Count; i++)
            {
                _flowRequestHandles[i].Complete();
            }
            _flowRequestHandles.Clear();
        }
        //AGENT MOV
        if (_agentMovementCalculationHandle.Count != 0)
        {
            _agentMovementCalculationHandle[0].Complete();
            _agentMovementCalculationHandle.Clear();
        }
        //ADD POR TRAV
        if(_additionPortalTravesalHandles.Count != 0)
        {
            for(int i = 0; i < _additionPortalTravesalHandles.Count; i++)
            {
                ExistingPathHandle existingPathHandle = _additionPortalTravesalHandles[i];
                existingPathHandle.Handle.Complete();
                ScheduleAdditionPathConstructionWithFlowRequests(existingPathHandle.PathIndex);
            }
            _additionPortalTravesalHandles.Clear();
        }
        //ADD PATH PROD
        if(_additionPathConstructionHandles.Count != 0)
        {
            for(int i = 0; i < _additionPathConstructionHandles.Count; i++)
            {
                ExistingPathHandle existingPathHandle = _additionPathConstructionHandles[i];
                existingPathHandle.Handle.Complete();
                _pathfindingManager.PathProducer.RefreshFlowFieldLength(existingPathHandle.PathIndex);
            }
            _additionPathConstructionHandles.Clear();
        }
        //TRANSFER NEW PATH INDICIES TO CUR PATH INDICIES
        NewPathToCurPathTransferJob newPathToCurPathTransferJob = new NewPathToCurPathTransferJob()
        {
            AgentDataArray = _pathfindingManager.AgentDataContainer.AgentDataList,
            AgentCurPathIndicies = _pathfindingManager.AgentDataContainer.AgentCurPathIndicies,
            AgentNewPathIndicies = _pathfindingManager.AgentDataContainer.AgentNewPathIndicies,
            PathSubscribers = _pathfindingManager.PathProducer.ProducedPathSubscribers,
        };
        newPathToCurPathTransferJob.Schedule().Complete();

        _pathfindingManager.PathProducer.TransferAllFlowFieldCalculationsToTheFlowFields();
        _pathfindingManager.PathProducer.DisposeFlowFieldCalculationBuffers();
        CurrentRequestedPaths.Clear();
        CurrentSourcePositions.Clear();
        SendRoutineResultsToAgents();
        sw.Stop();
        //if(sw.Elapsed.TotalMilliseconds > 1) { UnityEngine.Debug.Log(sw.Elapsed.TotalMilliseconds); }
    }
    public AgentRoutineDataProducer GetRoutineDataProducer()
    {
        return _dirCalculator;
    }
    void ScheduleAdditionPathConstructionWithFlowRequests(int pathIndex)
    {
        PathData path = ExistingPaths[pathIndex];
        NativeSlice<float2> sources = new NativeSlice<float2>(CurrentSourcePositions, path.FlowRequestSourceStart, path.FlowRequestSourceCount + path.PathAdditionSourceCount);
        ExistingPathHandle constructionHandle = _pathfindingManager.PathProducer.RequestAdditionPathConstruction(pathIndex, sources);
        _additionPathConstructionHandles.Add(constructionHandle);
    }
    void SchedulePathProduction(int pathIndex, NativeSlice<float2> sources)
    {
        NewPathHandle pathProdHandle = _pathfindingManager.PathProducer.SchedulePathProductionJob(pathIndex, sources);
        _pathProdCalcHandles.Add(pathProdHandle);
    }
    JobHandle ScheduleCostEditRequests(List<CostFieldEditJob[]> costFieldEditRequests)
    {
        NativeList<JobHandle> editHandles = new NativeList<JobHandle>(Allocator.Temp);
        JobHandle lastHandle = new JobHandle();
        if (costFieldEditRequests.Count != 0)
        {
            for (int j = 0; j < costFieldEditRequests[0].Length; j++)
            {
                CostFieldEditJob editJob = costFieldEditRequests[0][j];
                editJob.EditedSectorIndicies.Clear();
                editJob.EditedSectorIndexBorders.Clear();
                editJob.EditedSectorIndexBorders.Add(0);

                JobHandle editHandle = editJob.Schedule();
                editHandles.Add(editHandle);
            }
            lastHandle = JobHandle.CombineDependencies(editHandles);
            editHandles.Clear();
        }
        for (int i = 1; i < costFieldEditRequests.Count; i++)
        {
            for (int j = 0; j < costFieldEditRequests[i].Length; j++)
            {

                JobHandle editHandle = costFieldEditRequests[i][j].Schedule(lastHandle);
                editHandles.Add(editHandle);
            }
            lastHandle = JobHandle.CombineDependencies(editHandles);
            editHandles.Clear();
        }
        if (FlowFieldUtilities.DebugMode) { lastHandle.Complete(); }
        return lastHandle;
    }
    JobHandle ScheduleIslandFieldReconfig(JobHandle dependency)
    {
        JobHandle combinedHandles = new JobHandle();
        if (_costFieldEditRequestCount != 0)
        {
            IslandReconfigurationJob[] islandReconfigJobs = _pathfindingManager.FieldProducer.GetIslandReconfigJobs();
            NativeArray<JobHandle> handlesToCombine = new NativeArray<JobHandle>(islandReconfigJobs.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            for (int i = 0; i < islandReconfigJobs.Length; i++)
            {
                handlesToCombine[i] = islandReconfigJobs[i].Schedule(dependency);
            }
            combinedHandles = JobHandle.CombineDependencies(handlesToCombine);
        }

        if (FlowFieldUtilities.DebugMode) { combinedHandles.Complete(); }

        return combinedHandles;
    }
    void RunPathfindingDataOrganization()
    {
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
            PathfindingSources = CurrentSourcePositions,
            IslandFieldProcessors = islandFieldPorcessor,
            NewPaths = CurrentRequestedPaths,
            CurrentPaths = ExistingPaths,
            PathSubscribers = _pathfindingManager.PathProducer.ProducedPathSubscribers,
        };
        organization.Schedule().Complete();
        islandFieldPorcessor.Dispose();

        //SET PATH INDICIES OF REQUESTED PATHS
        for(int i = 0; i < CurrentRequestedPaths.Length; i++)
        {
            PathRequest currentpath = CurrentRequestedPaths[i];
            if (!currentpath.IsValid()) { continue; }
            NativeSlice<float2> pathSources = new NativeSlice<float2>(organization.PathfindingSources, currentpath.SourcePositionStartIndex, currentpath.AgentCount);
            NewPathHandle porTravHandle = SchedulePortalTraversal(pathSources, currentpath);
            porTravHandle.Soruces = pathSources;
            _porTravHandles.Add(porTravHandle);
            currentpath.PathIndex = porTravHandle.PathIndex;
            CurrentRequestedPaths[i] = currentpath;
        }

        //SET NEW PATH INDICIES OF AGENTS
        OrganizedAgentNewPathIndiciesSetJob newpathindiciesSetJob = new OrganizedAgentNewPathIndiciesSetJob()
        {
            AgentNewPathIndicies = newPathIndicies,
            RequestedPaths = CurrentRequestedPaths,
        };
        JobHandle newPathIndiciesHandle = newpathindiciesSetJob.Schedule();

        //SCHEDULE PATH ADDITIONS AND FLOW REQUESTS
        for(int i = 0; i < ExistingPaths.Length; i++)
        {
            PathData existingPath = ExistingPaths[i];
            if(existingPath.Task == 0) { continue; }
            NativeSlice<float2> flowRequestSources = new NativeSlice<float2>(organization.PathfindingSources, existingPath.FlowRequestSourceStart, existingPath.FlowRequestSourceCount);
            NativeSlice<float2> pathAdditionSources = new NativeSlice<float2>(organization.PathfindingSources, existingPath.PathAdditionSourceStart, existingPath.PathAdditionSourceCount);
            bool pathAdditionRequested = (existingPath.Task & PathTask.PathAdditionRequest) == PathTask.PathAdditionRequest;
            bool flowRequested = (existingPath.Task & PathTask.FlowRequest) == PathTask.FlowRequest;
            if (pathAdditionRequested)
            {
                ScheduleAdditionPotalTraversal(i, pathAdditionSources);

            }
            else if (flowRequested)
            {
                ScheduleFlowRequest(i, flowRequestSources);
            }
        }
        newPathIndiciesHandle.Complete();
    }
    void ScheduleAdditionPotalTraversal(int pathIndex, NativeSlice<float2> sources)
    {
        ExistingPathHandle handle = _pathfindingManager.PathProducer.RequestAdditionPortalTraversal(pathIndex, sources);

        if (FlowFieldUtilities.DebugMode) { handle.Handle.Complete(); }

        _additionPortalTravesalHandles.Add(handle);
    }
    void ScheduleFlowRequest(int pathIndex, NativeSlice<float2> sources)
    {
        JobHandle flowHandle = _pathfindingManager.PathProducer.RequestFlow(pathIndex, sources);

        if (FlowFieldUtilities.DebugMode) { flowHandle.Complete(); }

        _flowRequestHandles.Add(flowHandle);
    }
    void ScheduleAgentMovementJobs(JobHandle dependency)
    {
        //SCHEDULE MOV DATA CALC JOB
        AgentRoutineDataCalculationJob movDataJob = _dirCalculator.GetAgentMovementDataCalcJob();
        JobHandle movDataHandle = movDataJob.Schedule(movDataJob.AgentMovementData.Length, 64, dependency);

        //SCHEDULE WALL COLLISION JOB
        CollisionCalculationJob collisionJob = new CollisionCalculationJob()
        {
            DeltaTime = _pathfindingManager.AgentUpdateFrequency,
            TileSize = _pathfindingManager.TileSize,
            FieldColAmount = _pathfindingManager.ColumnAmount,
            FieldRowAmount = _pathfindingManager.RowAmount,
            VertexSequence = _pathfindingManager.FieldProducer.GetVertexSequence(),
            EdgeDirections = _pathfindingManager.FieldProducer.GetEdgeDirections(),
            TileToWallObject = _pathfindingManager.FieldProducer.GetTileToWallObject(),
            WallObjectList = _pathfindingManager.FieldProducer.GetWallObjectList(),
            AgentMovementData = _dirCalculator.AgentMovementDataList,
            RoutineResultArray = _dirCalculator.RoutineResults,
            AgentPositionChangeBuffer = _dirCalculator.AgentPositionChangeBuffer,
        };
        JobHandle collisionHandle = collisionJob.Schedule(collisionJob.AgentMovementData.Length, 64, movDataHandle);

        //SCHEDULE AGENT COLLISION JOB
        CollisionResolutionJob colResJob = new CollisionResolutionJob()
        {
            AgentMovementDataArray = _dirCalculator.AgentMovementDataList,
            AgentPositionChangeBuffer = _dirCalculator.AgentPositionChangeBuffer,
            HashGridArray = _dirCalculator.HashGridArray,
            SpatialGridUtils = new AgentSpatialGridUtils(0),
        };
        JobHandle colResHandle = colResJob.Schedule(colResJob.AgentMovementDataArray.Length, 4, collisionHandle);

        //SCHEDULE LOCAL AVODANCE JOB
        LocalAvoidanceJob avoidanceJob = new LocalAvoidanceJob()
        {
            SeperationMultiplier = BoidController.Instance.SeperationMultiplier,
            SeperationRangeAddition = BoidController.Instance.SeperationRangeAddition,
            SeekMultiplier = BoidController.Instance.SeekMultiplier,
            AlignmentMultiplier = BoidController.Instance.AlignmentMultiplier,
            AlignmentRangeAddition = BoidController.Instance.AlignmentRangeAddition,
            MovingAvoidanceRangeAddition = BoidController.Instance.MovingAvoidanceRangeAddition,
            BaseSpatialGridSize = FlowFieldUtilities.BaseSpatialGridSize,
            FieldHorizontalSize = FlowFieldUtilities.TileSize * FlowFieldUtilities.FieldColAmount,
            FieldVerticalSize = FlowFieldUtilities.TileSize * FlowFieldUtilities.FieldRowAmount,
            AgentMovementDataArray = _dirCalculator.AgentMovementDataList,
            RoutineResultArray = _dirCalculator.RoutineResults,
            HashGridArray = _dirCalculator.HashGridArray,
            SpatialGridUtils = new AgentSpatialGridUtils(0),
        };
        JobHandle avoidanceHandle = avoidanceJob.Schedule(avoidanceJob.AgentMovementDataArray.Length, 64, colResHandle);

        //SCHEDULE TENSON RES JOB
        TensionResolver tensionResJob = new TensionResolver()
        {
            HashGridArray = _dirCalculator.HashGridArray,
            HashGridUtils = new AgentSpatialGridUtils(0),
            RoutineResultArray = _dirCalculator.RoutineResults,
            AgentMovementDataArray = _dirCalculator.AgentMovementDataList,
            SeperationRangeAddition = BoidController.Instance.SeperationRangeAddition,
        };
        JobHandle tensionHandle = tensionResJob.Schedule(avoidanceHandle);


        if (FlowFieldUtilities.DebugMode) { tensionHandle.Complete(); }

        _agentMovementCalculationHandle.Add(tensionHandle);
    }
    NewPathHandle SchedulePortalTraversal(NativeSlice<float2> sources, PathRequest currentPath)
    {
        NewPathHandle pathHandle = _pathfindingManager.PathProducer.ConstructPath(sources, currentPath);

        if (FlowFieldUtilities.DebugMode)
        {
            pathHandle.Handle.Complete();
        }
        return pathHandle;        
    }

    
    public void SendRoutineResultsToAgents()
    {
        NativeArray<RoutineResult> routineResults = _dirCalculator.RoutineResults;
        NativeArray<AgentMovementData> agentMovementDataList = _dirCalculator.AgentMovementDataList;
        NativeArray<float2> agentPositionChangeBuffer = _dirCalculator.AgentPositionChangeBuffer;
        NativeArray<int> normalToHashed = _dirCalculator.NormalToHashed;

        _pathfindingManager.AgentDataContainer.SendRoutineResults(routineResults, agentMovementDataList, agentPositionChangeBuffer, normalToHashed);
    }
}