using Assets.Path_Finding_System.Scripts;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Jobs;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine.UIElements;
using static Unity.VisualScripting.Member;

public class RoutineScheduler
{
    PathfindingManager _pathfindingManager;
    AgentRoutineDataProducer _dirCalculator;
    PathConstructionPipeline _pathConstructionPipeline;

    List<JobHandle> _costEditHandle;
    List<JobHandle> _islandReconfigHandle;
    List<JobHandle> _agentMovementCalculationHandle;

    public NativeList<PathRequest> CurrentRequestedPaths;
    public NativeList<float2> CurrentSourcePositions;

    NativeList<UnsafeListReadOnly<byte>> _costFieldCosts;
    NativeList<SectorBitArray> EditedSectorBitArray;
    NativeList<ObstacleRequest> ObstacleRequests;
    NativeList<Obstacle> NewObstacles;
    public RoutineScheduler(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
        _dirCalculator = new AgentRoutineDataProducer(pathfindingManager.AgentDataContainer, pathfindingManager);
        _agentMovementCalculationHandle = new List<JobHandle>();
        _costEditHandle = new List<JobHandle>();
        CurrentRequestedPaths = new NativeList<PathRequest>(Allocator.Persistent);
        _islandReconfigHandle = new List<JobHandle>();
        CurrentSourcePositions = new NativeList<float2>(Allocator.Persistent);
        _pathConstructionPipeline = new PathConstructionPipeline(pathfindingManager);
        _costFieldCosts = new NativeList<UnsafeListReadOnly<byte>>(Allocator.Persistent);
        EditedSectorBitArray = new NativeList<SectorBitArray>(Allocator.Persistent);
        ObstacleRequests = new NativeList<ObstacleRequest>(Allocator.Persistent);
        NewObstacles = new NativeList<Obstacle>(Allocator.Persistent);
    }

    public void Schedule(NativeList<PathRequest> newPaths, NativeArray<ObstacleRequest>.ReadOnly obstacleRequests)
    {
        //COPY OBSTACLE REQUESTS
        ReadOnlyNativeArrayToNativeListCopyJob<ObstacleRequest> obstacleRequestCopy = new ReadOnlyNativeArrayToNativeListCopyJob<ObstacleRequest>()
        {
            Source = obstacleRequests,
            Destination = ObstacleRequests,
        };
        obstacleRequestCopy.Schedule().Complete();

        //REFRESH COST FIELD COSTS
        UnsafeListReadOnly<byte>[] costFielCosts = _pathfindingManager.GetAllCostFieldCostsAsUnsafeListReadonly();
        _costFieldCosts.Length = costFielCosts.Length;
        for(int i = 0; i < costFielCosts.Length; i++)
        {
            _costFieldCosts[i] = costFielCosts[i];
        }

        //SCHEDULE COST EDITS
        JobHandle costEditHandle = ScheduleCostEditRequests();
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

        _pathConstructionPipeline.ShcedulePathRequestEvalutaion(CurrentRequestedPaths, _costFieldCosts, EditedSectorBitArray.AsArray().AsReadOnly(), islandFieldReconfigHandle);
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
            }
        }

        _pathConstructionPipeline.TryComplete();
    }
    public void ForceCompleteAll()
    {
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
        }

        //AGENT MOV
        if (_agentMovementCalculationHandle.Count != 0)
        {
            _agentMovementCalculationHandle[0].Complete();
            _agentMovementCalculationHandle.Clear();
        }

        _pathConstructionPipeline.ForceComplete();

        //TRANSFER NEW PATH INDICIES TO CUR PATH INDICIES
        NewPathToCurPathTransferJob newPathToCurPathTransferJob = new NewPathToCurPathTransferJob()
        {
            AgentDataArray = _pathfindingManager.AgentDataContainer.AgentDataList,
            AgentCurPathIndicies = _pathfindingManager.AgentDataContainer.AgentCurPathIndicies,
            AgentNewPathIndicies = _pathfindingManager.AgentDataContainer.AgentNewPathIndicies,
            PathSubscribers = _pathfindingManager.PathContainer.ProducedPathSubscribers,
        };
        newPathToCurPathTransferJob.Schedule().Complete();
        CurrentRequestedPaths.Clear();
        CurrentSourcePositions.Clear();
        EditedSectorBitArray.Clear();
        ObstacleRequests.Clear();
        NewObstacles.Clear();
        SendRoutineResultsToAgents();
    }
    public AgentRoutineDataProducer GetRoutineDataProducer()
    {
        return _dirCalculator;
    }
    JobHandle ScheduleCostEditRequests()
    {
        if(ObstacleRequests.Length == 0) { return new JobHandle(); }

        NewObstacles.Length = ObstacleRequests.Length;
        ObstacleRequestToObstacleJob requestToObstacle = new ObstacleRequestToObstacleJob()
        {
            TileSize = FlowFieldUtilities.TileSize,
            FieldColAmount = FlowFieldUtilities.FieldColAmount,
            FieldRowAmount = FlowFieldUtilities.FieldRowAmount,
            NewObstacles = NewObstacles,
            ObstacleRequests = ObstacleRequests,
        };
        JobHandle toObstacleHandle = requestToObstacle.Schedule(NewObstacles.Length, 64);

        NativeList<JobHandle> editHandles = new NativeList<JobHandle>(Allocator.Temp);
        for(int i = 0; i <= FlowFieldUtilities.MaxCostFieldOffset; i++)
        {
            CostField costField = _pathfindingManager.FieldProducer.GetCostFieldWithOffset(i);
            FieldGraph fieldGraph = _pathfindingManager.FieldProducer.GetFieldGraphWithOffset(i);

            NativeListCopyJob<Obstacle> newObstaclesTransfer = new NativeListCopyJob<Obstacle>()
            {
                Source = NewObstacles,
                Destination = fieldGraph.NewObstacles,
            };
            JobHandle newObstacleTransferHandle = newObstaclesTransfer.Schedule(toObstacleHandle);

            CostFieldEditJob costEditJob = new CostFieldEditJob()
            {
                SectorColAmount = FlowFieldUtilities.SectorColAmount,
                SectorRowAmount = FlowFieldUtilities.SectorRowAmount,
                SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
                SectorMatrixRowAmount = FlowFieldUtilities.SectorMatrixRowAmount,
                FieldColAmount = FlowFieldUtilities.FieldColAmount,
                FieldRowAmount = FlowFieldUtilities.FieldRowAmount,
                SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
                Offset = i,
                SectorNodes = fieldGraph.SectorNodes,
                SecToWinPtrs = fieldGraph.SecToWinPtrs,
                AStarQueue = fieldGraph._aStarGrid._searchQueue,
                EditedSectorBits = fieldGraph.EditedSectorMarks,
                EditedSectorIndicies = fieldGraph.EditedSectorList,
                WinToSecPtrs = fieldGraph.WinToSecPtrs,
                CostsL = costField.CostsL,
                EditedWindowIndicies = fieldGraph.EditedWinodwList,
                EditedWindowMarks = fieldGraph.EditedWindowMarks,
                IntegratedCosts = fieldGraph.SectorIntegrationField,
                IslandFields = fieldGraph.IslandFields,
                Islands = fieldGraph.IslandDataList,
                NewObstacles = fieldGraph.NewObstacles,
                PorPtrs = fieldGraph.PorToPorPtrs,
                PortalNodes = fieldGraph.PortalNodes,
                PortalPerWindow = fieldGraph.PortalPerWindow,
                WindowNodes = fieldGraph.WindowNodes,
            };
            JobHandle editHandle = costEditJob.Schedule(newObstacleTransferHandle);
            editHandles.Add(editHandle);

            EditedSectorBitArray.Add(costEditJob.EditedSectorBits);
        }
        JobHandle cominedHandle = JobHandle.CombineDependencies(editHandles);
        editHandles.Dispose();

        if (FlowFieldUtilities.DebugMode) { cominedHandle.Complete(); }
        return cominedHandle;
    }
    JobHandle ScheduleIslandFieldReconfig(JobHandle dependency)
    {
        if(NewObstacles.Length == 0) { return new JobHandle(); }

        NativeArray<JobHandle> handlesToCombine = new NativeArray<JobHandle>(FlowFieldUtilities.MaxCostFieldOffset + 1, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        for (int i = 0; i <= FlowFieldUtilities.MaxCostFieldOffset; i++)
        {
            FieldGraph fieldGraph = _pathfindingManager.FieldProducer.GetFieldGraphWithOffset(i);
            CostField costField = _pathfindingManager.FieldProducer.GetCostFieldWithOffset(i);

            IslandReconfigurationJob islandReconfig = new IslandReconfigurationJob()
            {
                Offset = i,
                SectorColAmount = FlowFieldUtilities.SectorColAmount,
                SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
                SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
                SectorNodes = fieldGraph.SectorNodes,
                SecToWinPtrs = fieldGraph.SecToWinPtrs,
                EditedSectorIndicies = fieldGraph.EditedSectorList,
                PortalEdges = fieldGraph.PorToPorPtrs,
                CostsL = costField.CostsL,
                IslandFields = fieldGraph.IslandFields,
                Islands = fieldGraph.IslandDataList,
                PortalNodes = fieldGraph.PortalNodes,
                WindowNodes = fieldGraph.WindowNodes,
            };
            handlesToCombine[i] = islandReconfig.Schedule(dependency);
        }
        JobHandle combinedHandles = JobHandle.CombineDependencies(handlesToCombine);

        if (FlowFieldUtilities.DebugMode) { combinedHandles.Complete(); }

        return combinedHandles;
    }
    void ScheduleAgentMovementJobs(JobHandle dependency)
    {
        //SCHEDULE MOV DATA CALC JOB
        AgentRoutineDataCalculationJob movDataJob = _dirCalculator.GetAgentMovementDataCalcJob();
        JobHandle movDataHandle = movDataJob.Schedule(movDataJob.AgentMovementData.Length, 64, dependency);

        //SCHEDULE AGENT COLLISION JOB
        CollisionResolutionJob colResJob = new CollisionResolutionJob()
        {
            AgentMovementDataArray = _dirCalculator.AgentMovementDataList,
            AgentPositionChangeBuffer = _dirCalculator.AgentPositionChangeBuffer,
            HashGridArray = _dirCalculator.HashGridArray,
            SpatialGridUtils = new AgentSpatialGridUtils(0),
        };
        JobHandle colResHandle = colResJob.Schedule(colResJob.AgentMovementDataArray.Length, 4, movDataHandle);

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

        //SCHEDULE WALL COLLISION JOB

        AgentWallCollisionJob wallCollision = new AgentWallCollisionJob()
        {
            SectorColAmount = FlowFieldUtilities.SectorColAmount,
            SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
            SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
            TileSize = FlowFieldUtilities.TileSize,
            FieldColAmount = FlowFieldUtilities.FieldColAmount,
            FieldRowAmount = FlowFieldUtilities.FieldRowAmount,
            SectorRowAmount = FlowFieldUtilities.SectorRowAmount,
            HalfTileSize = FlowFieldUtilities.TileSize / 2,
            AgentMovementData = _dirCalculator.AgentMovementDataList,
            AgentPositionChangeBuffer = _dirCalculator.AgentPositionChangeBuffer,
            CostFieldEachOffset = _costFieldCosts,
        };
        JobHandle wallCollisionHandle = wallCollision.Schedule(wallCollision.AgentMovementData.Length, 64, tensionHandle);


        if (FlowFieldUtilities.DebugMode) { wallCollisionHandle.Complete(); }

        _agentMovementCalculationHandle.Add(wallCollisionHandle);
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