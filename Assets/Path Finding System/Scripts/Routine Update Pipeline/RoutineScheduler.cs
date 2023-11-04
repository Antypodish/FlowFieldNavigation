using Assets.Path_Finding_System.Scripts;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Jobs;
using static UnityEngine.Rendering.VirtualTexturing.Debugging;

public class RoutineScheduler
{
    PathfindingManager _pathfindingManager;
    AgentRoutineDataProducer _dirCalculator;

    List<PathHandle> _pathProdCalcHandles;
    NativeList<JobHandle> _pathAdditionHandles;

    //PREDECESSOR HANDLES (Handles having successors wich depens on the output of these jobs)
    List<PathHandle> _porTravHandles;
    List<PathHandle> _porAddTravHandles;
    List<JobHandle> _movDataCalcHandle;
    List<JobHandle> _colCalculationHandle;
    List<JobHandle> _avoidanceHandle;
    List<JobHandle> _collisionResolutionHandle;
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
    }

    public void Schedule(List<CostFieldEditJob[]> costEditJobs, IslandReconfigurationJob[] islandReconfigJobs, List<PortalTraversalJobPack> portalTravRequests)
    {
        JobHandle costEditHandle = ScheduleCostEditRequests(costEditJobs, islandReconfigJobs);
        AddMovementDataCalculationHandle(costEditHandle);
        AddCollisionResolutionJob();
        AddLocalAvoidanceJob();
        AddCollisionCalculationJob();
        //_schedulingTree.SetPortalAdditionTraversalHandles();
        AddPortalTraversalHandles(portalTravRequests, costEditHandle);
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
            for(int i = 0; i < 1; i++)
            {
                editHandles.Add(islandReconfigJobs[i].Schedule(lastHandle));
            }
            lastHandle = JobHandle.CombineDependencies(editHandles);
            editHandles.Clear();
        }

        if (FlowFieldUtilities.DebugMode) { lastHandle.Complete(); }
        return lastHandle;
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
    void AddPortalTraversalHandles(List<PortalTraversalJobPack> portalTravJobs, JobHandle dependency)
    {
        for (int i = 0; i < portalTravJobs.Count; i++)
        {
            if (portalTravJobs[i].Path == null) { continue; }
            _porTravHandles.Add(portalTravJobs[i].Schedule(dependency));
        }
        portalTravJobs.Clear();

        if (FlowFieldUtilities.DebugMode)
        {            
            for (int i = 0; i < _porTravHandles.Count; i++)
            {
                _porTravHandles[i].Handle.Complete();

            }
        }
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
        //HANDLE PORTAL TRAVERSALS
        for (int i = _porTravHandles.Count - 1; i >= 0; i--)
        {
            PathHandle handle = _porTravHandles[i];
            if (handle.Handle.IsCompleted)
            {
                handle.Handle.Complete();   
                _pathProdCalcHandles.Add(_pathfindingManager.PathProducer.SchedulePathProductionJob(handle.Path));
                _porTravHandles.RemoveAtSwapBack(i);
            }
        }

        if (_movDataCalcHandle.Count != 0 && _movDataCalcHandle[0].IsCompleted)
        {
            _movDataCalcHandle[0].Complete();
            _movDataCalcHandle.Clear();
        }

        //HANDLE PORTAL ADD TRAVERSALS
        for (int i = _porAddTravHandles.Count - 1; i >= 0; i--)
        {
            PathHandle handle = _porAddTravHandles[i];

            if (handle.Handle.IsCompleted)
            {
                handle.Handle.Complete();
                _porAddTravHandles.RemoveAtSwapBack(i);

                if (handle.Path.IntegrationStartIndicies.Length != 0)
                {
                    JobHandle additionHandle = _pathfindingManager.PathProducer.SchedulePathAdditionJob(handle.Path);
                    _pathAdditionHandles.Add(additionHandle);
                }
            }
        }

        if (FlowFieldUtilities.DebugMode)
        {
            for (int i = 0; i < _pathProdCalcHandles.Count; i++)
            {
                _pathProdCalcHandles[i].Handle.Complete();
            }
            JobHandle.CompleteAll(_pathAdditionHandles);
        }
    }
    public void ForceCompleteAll()
    {
        //FORCE COMPLETE MOVEMENT DATA CALCULATION
        if (_movDataCalcHandle.Count == 1)
        {
            _movDataCalcHandle[0].Complete();
            _movDataCalcHandle.Clear();
        }

        //FORCE COMPLETE COLLISION RESOLUTION
        if (_collisionResolutionHandle.Count != 0)
        {
            _collisionResolutionHandle[0].Complete();
            _collisionResolutionHandle.Clear();
        }

        //FORCE COMPLETE LOCAL AVOIDANCE
        if (_avoidanceHandle.Count != 0)
        {
            _avoidanceHandle[0].Complete();
            _avoidanceHandle.Clear();
        }

        //FORCE COMPLETE COLLISION CALCULATION
        if (_colCalculationHandle.Count != 0)
        {
            _colCalculationHandle[0].Complete();
            _colCalculationHandle.Clear();
        }
        //FOCE COMTPLETE PATH PRODUCTION TRAVERSALS
        for (int i = 0; i < _porTravHandles.Count; i++)
        {
            PathHandle handle = _porTravHandles[i];
            handle.Handle.Complete();
            _pathProdCalcHandles.Add(_pathfindingManager.PathProducer.SchedulePathProductionJob(handle.Path));
        }
        _porTravHandles.Clear();

        //FORCE COMTPLETE PATH PRODUCTIONS
        for (int i = 0; i < _pathProdCalcHandles.Count; i++)
        {
            PathHandle handle = _pathProdCalcHandles[i];
            handle.Handle.Complete();
            handle.Path.IsCalculated = true;
        }
        _pathProdCalcHandles.Clear();

        //FORCE COMPLETE PORTAL ADDITION TRAVERSALS
        for (int i = _porAddTravHandles.Count - 1; i >= 0; i--)
        {
            PathHandle handle = _porAddTravHandles[i];
            handle.Handle.Complete();
            JobHandle additionHandle = _pathfindingManager.PathProducer.SchedulePathAdditionJob(handle.Path);
            _pathAdditionHandles.Add(additionHandle);
        }
        _porAddTravHandles.Clear();

        //FORCE COMPLETE PATH ADDITIONS
        JobHandle.CompleteAll(_pathAdditionHandles);
        _pathAdditionHandles.Clear();

        //SEND DIRECTIONS
        SendRoutineResultsToAgents();
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