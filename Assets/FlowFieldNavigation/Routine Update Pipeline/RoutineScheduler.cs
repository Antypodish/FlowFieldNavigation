using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Jobs;

public class RoutineScheduler
{
    PathfindingManager _pathfindingManager;
    MovementIO _movementIO;
    PathConstructionPipeline _pathConstructionPipeline;

    List<JobHandle> _costEditHandle;
    List<JobHandle> _islandReconfigHandle;

    public NativeList<PathRequest> CurrentRequestedPaths;
    public NativeList<float2> CurrentSourcePositions;

    NativeList<UnsafeListReadOnly<byte>> _costFieldCosts;
    NativeList<SectorBitArray> EditedSectorBitArray;
    NativeList<CostEdit> NewCostEditRequests;
    public RoutineScheduler(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
        _movementIO = new MovementIO(pathfindingManager.AgentDataContainer, pathfindingManager);
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
        NativeArray<IslandFieldProcessor> islandFieldProcessors = _pathfindingManager.FieldProducer.GetAllIslandFieldProcessors();

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


        _pathConstructionPipeline.ShcedulePathRequestEvalutaion(CurrentRequestedPaths, _costFieldCosts, EditedSectorBitArray.AsArray().AsReadOnly(), islandFieldProcessors, islandFieldReconfigHandle);
        _movementIO.ScheduleRoutine(_costFieldCosts, costEditHandle);
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

        _movementIO.ForceComplete();
        _pathConstructionPipeline.ForceComplete();
        SendRoutineResultsToAgents();

        //TRANSFER NEW PATH INDICIES TO CUR PATH INDICIES
        NewPathToCurPathTransferJob newPathToCurPathTransferJob = new NewPathToCurPathTransferJob()
        {
            AgentDestinationReachedArray = _pathfindingManager.AgentDataContainer.AgentDestinationReachedArray,
            AgentDataArray = _pathfindingManager.AgentDataContainer.AgentDataList,
            AgentCurPathIndicies = _pathfindingManager.AgentDataContainer.AgentCurPathIndicies,
            AgentNewPathIndicies = _pathfindingManager.AgentDataContainer.AgentNewPathIndicies,
            PathSubscribers = _pathfindingManager.PathContainer.PathSubscriberCounts,
        };
        newPathToCurPathTransferJob.Schedule().Complete();

        CurrentRequestedPaths.Clear();
        CurrentSourcePositions.Clear();
        EditedSectorBitArray.Clear();
        NewCostEditRequests.Clear();
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

    public void SendRoutineResultsToAgents()
    {
        NativeArray<RoutineResult> routineResults = _movementIO.RoutineResults;
        NativeArray<AgentMovementData> agentMovementDataList = _movementIO.AgentMovementDataList;
        NativeArray<float3> agentPositionChangeBuffer = _movementIO.AgentPositionChangeBuffer;
        NativeArray<int> normalToHashed = _movementIO.NormalToHashed;

        _pathfindingManager.AgentDataContainer.SendRoutineResults(routineResults, agentMovementDataList, agentPositionChangeBuffer, normalToHashed);
    }
}