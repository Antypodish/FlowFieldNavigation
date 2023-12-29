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
    MovementIO _movementIO;
    PathConstructionPipeline _pathConstructionPipeline;

    List<JobHandle> _costEditHandle;
    List<JobHandle> _islandReconfigHandle;
    List<JobHandle> _agentMovementCalculationHandle;

    public NativeList<PathRequest> CurrentRequestedPaths;
    public NativeList<float2> CurrentSourcePositions;

    NativeList<UnsafeListReadOnly<byte>> _costFieldCosts;
    NativeList<SectorBitArray> EditedSectorBitArray;
    NativeList<CostEdit> NewCostEditRequests;
    public RoutineScheduler(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
        _movementIO = new MovementIO(pathfindingManager.AgentDataContainer, pathfindingManager);
        _agentMovementCalculationHandle = new List<JobHandle>();
        _costEditHandle = new List<JobHandle>();
        CurrentRequestedPaths = new NativeList<PathRequest>(Allocator.Persistent);
        _islandReconfigHandle = new List<JobHandle>();
        CurrentSourcePositions = new NativeList<float2>(Allocator.Persistent);
        _pathConstructionPipeline = new PathConstructionPipeline(pathfindingManager);
        _costFieldCosts = new NativeList<UnsafeListReadOnly<byte>>(Allocator.Persistent);
        EditedSectorBitArray = new NativeList<SectorBitArray>(Allocator.Persistent);
        NewCostEditRequests = new NativeList<CostEdit>(Allocator.Persistent);
    }

    public void Schedule(NativeList<PathRequest> newPaths, NativeArray<CostEdit>.ReadOnly costEditRequests)
    {
        //COPY OBSTACLE REQUESTS
        ReadOnlyNativeArrayToNativeListCopyJob<CostEdit> obstacleRequestCopy = new ReadOnlyNativeArrayToNativeListCopyJob<CostEdit>()
        {
            Source = costEditRequests,
            Destination = NewCostEditRequests,
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
            MaxXExcluding = FlowFieldUtilities.FieldMaxXExcluding,
            MaxYExcluding = FlowFieldUtilities.FieldMaxYExcluding,
            MinXIncluding = FlowFieldUtilities.FieldMinXIncluding,
            MinYIncluding = FlowFieldUtilities.FieldMinYIncluding,
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
        NewCostEditRequests.Clear();
        SendRoutineResultsToAgents();
    }
    public MovementIO GetRoutineDataProducer()
    {
        return _movementIO;
    }
    JobHandle ScheduleCostEditRequests()
    {
        if(NewCostEditRequests.Length == 0) { return new JobHandle(); }

        NativeList<JobHandle> editHandles = new NativeList<JobHandle>(Allocator.Temp);
        for(int i = 0; i <= FlowFieldUtilities.MaxCostFieldOffset; i++)
        {
            CostField costField = _pathfindingManager.FieldProducer.GetCostFieldWithOffset(i);
            FieldGraph fieldGraph = _pathfindingManager.FieldProducer.GetFieldGraphWithOffset(i);

            NativeListCopyJob<CostEdit> newObstaclesTransfer = new NativeListCopyJob<CostEdit>()
            {
                Source = NewCostEditRequests,
                Destination = fieldGraph.NewCostEdits,
            };
            JobHandle newObstacleTransferHandle = newObstaclesTransfer.Schedule();

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
                EditedSectorBits = fieldGraph.EditedSectorMarks,
                EditedSectorIndicies = fieldGraph.EditedSectorList,
                WinToSecPtrs = fieldGraph.WinToSecPtrs,
                Costs = costField.Costs,
                CostStamps = costField.StampCounts,
                BaseCosts = costField.BaseCosts,
                EditedWindowIndicies = fieldGraph.EditedWinodwList,
                EditedWindowMarks = fieldGraph.EditedWindowMarks,
                IntegratedCosts = fieldGraph.SectorIntegrationField,
                IslandFields = fieldGraph.IslandFields,
                Islands = fieldGraph.IslandDataList,
                NewCostEdits = fieldGraph.NewCostEdits,
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
        if(NewCostEditRequests.Length == 0) { return new JobHandle(); }

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
                CostsL = costField.Costs,
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
        JobHandle handle = _movementIO.PrepareAgentMovementDataCalculationJob(dependency);
        //SCHEDULE MOV DATA CALC JOB
        AgentRoutineDataCalculationJob movDataJob = _movementIO.GetAgentMovementDataCalcJob();
        JobHandle movDataHandle = movDataJob.Schedule(movDataJob.AgentMovementData.Length, 64, handle);
        //SCHEDULE AGENT COLLISION JOB
        CollisionResolutionJob colResJob = new CollisionResolutionJob()
        {
            AgentMovementDataArray = _movementIO.AgentMovementDataList,
            AgentPositionChangeBuffer = _movementIO.AgentPositionChangeBuffer,
            HashGridArray = _movementIO.HashGridArray,
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
            AgentMovementDataArray = _movementIO.AgentMovementDataList,
            RoutineResultArray = _movementIO.RoutineResults,
            HashGridArray = _movementIO.HashGridArray,
            SpatialGridUtils = new AgentSpatialGridUtils(0),
        };
        JobHandle avoidanceHandle = avoidanceJob.Schedule(avoidanceJob.AgentMovementDataArray.Length, 64, colResHandle);

        //SCHEDULE TENSON RES JOB
        TensionResolver tensionResJob = new TensionResolver()
        {
            HashGridArray = _movementIO.HashGridArray,
            HashGridUtils = new AgentSpatialGridUtils(0),
            RoutineResultArray = _movementIO.RoutineResults,
            AgentMovementDataArray = _movementIO.AgentMovementDataList,
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
            AgentMovementData = _movementIO.AgentMovementDataList,
            AgentPositionChangeBuffer = _movementIO.AgentPositionChangeBuffer,
            CostFieldEachOffset = _costFieldCosts,
        };
        JobHandle wallCollisionHandle = wallCollision.Schedule(wallCollision.AgentMovementData.Length, 64, tensionHandle);


        if (FlowFieldUtilities.DebugMode) { wallCollisionHandle.Complete(); }

        _agentMovementCalculationHandle.Add(wallCollisionHandle);
    }


    public void SendRoutineResultsToAgents()
    {
        NativeArray<RoutineResult> routineResults = _movementIO.RoutineResults;
        NativeArray<AgentMovementData> agentMovementDataList = _movementIO.AgentMovementDataList;
        NativeArray<float2> agentPositionChangeBuffer = _movementIO.AgentPositionChangeBuffer;
        NativeArray<int> normalToHashed = _movementIO.NormalToHashed;

        _pathfindingManager.AgentDataContainer.SendRoutineResults(routineResults, agentMovementDataList, agentPositionChangeBuffer, normalToHashed);
    }
}