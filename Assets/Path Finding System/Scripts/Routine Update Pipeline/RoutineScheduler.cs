using Assets.Path_Finding_System.Scripts;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Jobs;

public class RoutineScheduler
{
    PathfindingManager _pathfindingManager;
    AgentRoutineDataProducer _dirCalculator;

    List<PathHandle> _pathProdCalcHandles;
    NativeList<JobHandle> _pathAdditionHandles;

    List<JobHandle> _costEditHandle;
    List<PathHandle> _porTravHandles;
    List<PathHandle> _porAddTravHandles;
    List<JobHandle> _movDataCalcHandle;
    List<JobHandle> _colCalculationHandle;
    List<JobHandle> _avoidanceHandle;
    List<JobHandle> _collisionResolutionHandle;

    public NativeList<PathRequest> CurrentRequestedPaths;
    public RoutineScheduler(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
        _dirCalculator = new AgentRoutineDataProducer(pathfindingManager.AgentDataContainer, pathfindingManager);

        _porTravHandles = new List<PathHandle>();
        _porAddTravHandles = new List<PathHandle>();
        _movDataCalcHandle = new List<JobHandle>();
        _pathProdCalcHandles = new List<PathHandle>();
        _pathAdditionHandles = new NativeList<JobHandle>(Allocator.Persistent);
        _colCalculationHandle = new List<JobHandle>();
        _avoidanceHandle = new List<JobHandle>();
        _collisionResolutionHandle = new List<JobHandle>();
        _costEditHandle = new List<JobHandle>();
        CurrentRequestedPaths = new NativeList<PathRequest>(Allocator.Persistent);
    }

    public void Schedule(List<CostFieldEditJob[]> costEditJobs, IslandReconfigurationJob[] islandReconfigJobs, NativeList<PathRequest> newPaths)
    {
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
        JobHandle.CombineDependencies(transferHandle, copyHandle).Complete();
        //SCHEDULE COST EDITS
        JobHandle costEditHandle = ScheduleCostEditRequests(costEditJobs, islandReconfigJobs);
        _costEditHandle.Add(costEditHandle);
    }
    public AgentRoutineDataProducer GetRoutineDataProducer()
    {
        return _dirCalculator;
    }
    JobHandle ScheduleCostEditRequests(List<CostFieldEditJob[]> costFieldEditRequests, IslandReconfigurationJob[] islandReconfigJobs)
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

        if(costFieldEditRequests.Count != 0)
        {
            for(int i = 0; i < islandReconfigJobs.Length; i++)
            {
                editHandles.Add(islandReconfigJobs[i].Schedule(lastHandle));
            }
            lastHandle = JobHandle.CombineDependencies(editHandles);
            editHandles.Clear();
        }
        if (FlowFieldUtilities.DebugMode) { lastHandle.Complete(); }
        return lastHandle;
    }
    void RunPathfindingDataOrganization()
    {
        if (CurrentRequestedPaths.IsEmpty) { return; }

        NativeArray<AgentData> agentData = _pathfindingManager.AgentDataContainer.AgentDataList;
        NativeArray<int> newPathIndicies = _pathfindingManager.AgentDataContainer.AgentNewPathIndicies;
        NativeArray<IslandFieldProcessor> islandFieldPorcessor = _pathfindingManager.FieldProducer.GetAllIslandFieldProcessors();
        TransformAccessArray agentTransforms = _pathfindingManager.AgentDataContainer.AgentTransforms;

        AgentDataSetPositionJob posSetJob = new AgentDataSetPositionJob()
        {
            AgentDataArray = agentData,
        };
        posSetJob.Schedule(agentTransforms).Complete();

        PathfindingTaskOrganizationJob organization = new PathfindingTaskOrganizationJob()
        {
            TileSize = FlowFieldUtilities.TileSize,
            AgentData = agentData,
            AgentNewPathIndicies = newPathIndicies,
            PathRequestSources = new NativeList<float2>(Allocator.Persistent),
            IslandFieldProcessors = islandFieldPorcessor,
            NewPaths = CurrentRequestedPaths,
        };
        organization.Schedule().Complete();

        //SET PATH INDICIES OF REQUESTED PATHS
        for(int i = 0; i < CurrentRequestedPaths.Length; i++)
        {
            PathRequest currentpath = CurrentRequestedPaths[i];
            if (!currentpath.IsValid()) { continue; }
            NativeSlice<float2> pathSources = new NativeSlice<float2>(organization.PathRequestSources, currentpath.SourcePositionStartIndex, currentpath.AgentCount);
            PortalTraversalJobPack porTravJobPack = _pathfindingManager.PathProducer.ConstructPath(pathSources, currentpath);
            PathHandle porTravHandle = SchedulePortalTraversal(porTravJobPack);
            _porTravHandles.Add(porTravHandle);
            currentpath.PathIndex = porTravJobPack.PathIndex;
            CurrentRequestedPaths[i] = currentpath;
        }

        //SET NEW PATH INDICIES OF AGENTS
        OrganizedAgentNewPathIndiciesSetJob newpathindiciesSetJob = new OrganizedAgentNewPathIndiciesSetJob()
        {
            AgentNewPathIndicies = newPathIndicies,
            CurrentRequestedPaths = CurrentRequestedPaths,
        };
        newpathindiciesSetJob.Schedule().Complete();
    }
    void AddMovementDataCalculationHandle(JobHandle dependency)
    {
        AgentRoutineDataCalculationJob movDataJob = _dirCalculator.CalculateDirections();
        _movDataCalcHandle.Add(movDataJob.Schedule(movDataJob.AgentMovementData.Length, 64, dependency));

        if (FlowFieldUtilities.DebugMode) { _movDataCalcHandle[_movDataCalcHandle.Count - 1].Complete(); }
    }
    void AddCollisionResolutionJob()
    {
        CollisionResolutionJob colResJob = new CollisionResolutionJob()
        {
            AgentMovementDataArray = _dirCalculator.AgentMovementDataList,
            AgentPositionChangeBuffer = _dirCalculator.AgentPositionChangeBuffer,
            HashGridArray = _dirCalculator.HashGridArray,
            SpatialGridUtils = new AgentSpatialGridUtils(0),
        };
        JobHandle colResHandle = colResJob.Schedule(colResJob.AgentMovementDataArray.Length, 4, _movDataCalcHandle[0]);
        _collisionResolutionHandle.Add(colResHandle);

        if (FlowFieldUtilities.DebugMode) { _collisionResolutionHandle[0].Complete(); }
    }
    void AddLocalAvoidanceJob()
    {
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
        JobHandle avoidanceHandle = avoidanceJob.Schedule(avoidanceJob.AgentMovementDataArray.Length, 64, _collisionResolutionHandle[0]);
        TensionResolver tensionResJob = new TensionResolver()
        {
            HashGridArray = _dirCalculator.HashGridArray,
            HashGridUtils = new AgentSpatialGridUtils(0),
            RoutineResultArray = _dirCalculator.RoutineResults,
            AgentMovementDataArray = _dirCalculator.AgentMovementDataList,
            SeperationRangeAddition = BoidController.Instance.SeperationRangeAddition,
        };
        JobHandle tensionHandle = tensionResJob.Schedule(avoidanceHandle);

        _avoidanceHandle.Add(tensionHandle);

        if (FlowFieldUtilities.DebugMode) { _avoidanceHandle[0].Complete(); }
    }
    void AddCollisionCalculationJob()
    {
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
        JobHandle collisionHandle = collisionJob.Schedule(collisionJob.AgentMovementData.Length, 64, _avoidanceHandle[0]);
        _colCalculationHandle.Add(collisionHandle);

        if (FlowFieldUtilities.DebugMode) { _colCalculationHandle[0].Complete(); }
    }
    PathHandle SchedulePortalTraversal(PortalTraversalJobPack portalTravJobPack)
    {
        PathHandle pathHandle = portalTravJobPack.Schedule();

        if (FlowFieldUtilities.DebugMode)
        {
            pathHandle.Handle.Complete();
        }
        return pathHandle;        
    }
    void SetPortalAdditionTraversalHandles()
    {
        _pathfindingManager.PathProducer.SetPortalAdditionTraversalHandles(_dirCalculator.AgentOutOfFieldStatusList, _porAddTravHandles, _movDataCalcHandle[0]);

        if (FlowFieldUtilities.DebugMode)
        {
            for (int i = 0; i < _porAddTravHandles.Count; i++)
            {
                _porAddTravHandles[i].Handle.Complete();
            }
        }

    }
    public void TryCompletePredecessorJobs()
    {
        if (_costEditHandle.Count != 0)
        {
            if (_costEditHandle[0].IsCompleted)
            {
                _costEditHandle[0].Complete();
                _costEditHandle.RemoveAtSwapBack(0);

                RunPathfindingDataOrganization();
                return;
            }
        }
        if(_porTravHandles.Count != 0)
        {
            for(int i = 0; i < _porTravHandles.Count; i++)
            {
                PathHandle pathHandle = _porTravHandles[i];
                if (pathHandle.Handle.IsCompleted)
                {
                    pathHandle.Handle.Complete();
                    _pathfindingManager.PathProducer.ProducedPaths[pathHandle.PathIndex].IsCalculated = true;
                    _porTravHandles.RemoveAtSwapBack(i);
                }
            }
        }
    }
    public void ForceCompleteAll()
    {
        
        //FOCE COMTPLETE COST EDIT
        if (_costEditHandle.Count != 0)
        {
            _costEditHandle[0].Complete();
            _costEditHandle.RemoveAtSwapBack(0);

            RunPathfindingDataOrganization();
        }

        
        if (_porTravHandles.Count != 0)
        {
            for (int i = 0; i < _porTravHandles.Count; i++)
            {
                PathHandle pathHandle = _porTravHandles[i];
                pathHandle.Handle.Complete();
                _pathfindingManager.PathProducer.ProducedPaths[pathHandle.PathIndex].IsCalculated = true;
            }
            _porTravHandles.Clear();
        }


        //TRANSFER NEW PATH INDICIES TO CUR PATH INDICIES
        NewPathToCurPathTransferJob newPathToCurPathTransferJob = new NewPathToCurPathTransferJob()
        {
            AgentCurPathIndicies = _pathfindingManager.AgentDataContainer.AgentCurPathIndicies,
            AgentNewPathIndicies = _pathfindingManager.AgentDataContainer.AgentNewPathIndicies,
            PathSubscribers = _pathfindingManager.PathProducer.ProducedPathSubscribers,
        };
        
        newPathToCurPathTransferJob.Schedule().Complete();
        

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