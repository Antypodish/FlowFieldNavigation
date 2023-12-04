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
    PathConstructionPipeline _pathConstructionPipeline;

    List<JobHandle> _costEditHandle;
    List<JobHandle> _islandReconfigHandle;
    List<JobHandle> _agentMovementCalculationHandle;

    public NativeList<PathRequest> CurrentRequestedPaths;
    public NativeList<float2> CurrentSourcePositions;
    public NativeList<PathData> ExistingPaths;

    int _costFieldEditRequestCount = 0;
    public RoutineScheduler(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
        _dirCalculator = new AgentRoutineDataProducer(pathfindingManager.AgentDataContainer, pathfindingManager);
        _agentMovementCalculationHandle = new List<JobHandle>();
        _costEditHandle = new List<JobHandle>();
        CurrentRequestedPaths = new NativeList<PathRequest>(Allocator.Persistent);
        _islandReconfigHandle = new List<JobHandle>();
        CurrentSourcePositions = new NativeList<float2>(Allocator.Persistent);
        ExistingPaths = new NativeList<PathData>(Allocator.Persistent);
        _pathConstructionPipeline = new PathConstructionPipeline(pathfindingManager);
    }

    public void Schedule(List<CostFieldEditJob[]> costEditJobs, NativeList<PathRequest> newPaths)
    {
        _costFieldEditRequestCount = costEditJobs.Count;
        _pathfindingManager.PathProducer.GetCurrentPathData(ExistingPaths);

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

                _pathConstructionPipeline.EvaluatePathRequests(CurrentRequestedPaths);
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
            _pathConstructionPipeline.EvaluatePathRequests(CurrentRequestedPaths);
        }
        _pathConstructionPipeline.ForceComplete();

        //AGENT MOV
        if (_agentMovementCalculationHandle.Count != 0)
        {
            _agentMovementCalculationHandle[0].Complete();
            _agentMovementCalculationHandle.Clear();
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
        CurrentRequestedPaths.Clear();
        CurrentSourcePositions.Clear();
        SendRoutineResultsToAgents();
    }
    public AgentRoutineDataProducer GetRoutineDataProducer()
    {
        return _dirCalculator;
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
        JobHandle collisionHandle = collisionJob.Schedule(collisionJob.AgentMovementData.Length, 64, tensionHandle);


        if (FlowFieldUtilities.DebugMode) { collisionHandle.Complete(); }

        _agentMovementCalculationHandle.Add(collisionHandle);
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