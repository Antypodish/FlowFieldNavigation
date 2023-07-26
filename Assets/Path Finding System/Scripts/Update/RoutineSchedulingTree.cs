using System.Collections.Generic;
using System.Threading;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine.Jobs;

public class RoutineSchedulingTree
{
    PathfindingManager _pathfindingManager;
    AgentDirectionCalculator _dirCalculator;

    List<PathHandle> _pathProdCalcHandles;
    NativeList<JobHandle> _pathAdditionHandles;

    //PREDECESSOR HANDLES (Handles having successors wich depens on the output of these jobs)
    List<PathHandle> _porTravHandles;
    List<PathHandle> _porAddTravHandles;
    List<JobHandle> _movDataCalcHandle;

    public RoutineSchedulingTree(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
        _dirCalculator = new AgentDirectionCalculator(pathfindingManager.AgentDataContainer, pathfindingManager);

        _porTravHandles = new List<PathHandle>();
        _porAddTravHandles = new List<PathHandle>();
        _movDataCalcHandle = new List<JobHandle>();
        _pathProdCalcHandles = new List<PathHandle>();
        _pathAdditionHandles = new NativeList<JobHandle>(Allocator.Persistent);
    }

    public JobHandle ScheduleCostEditRequests(List<CostFieldEditJob[]> costFieldEditRequests)
    {
        NativeList<JobHandle> editHandles = new NativeList<JobHandle>(Allocator.Temp);
        JobHandle lastHandle = new JobHandle();

        if (costFieldEditRequests.Count != 0)
        {
            for (int j = 0; j < costFieldEditRequests[0].Length; j++)
            {
                editHandles.Add(costFieldEditRequests[0][j].Schedule());
            }
            lastHandle = JobHandle.CombineDependencies(editHandles);
            editHandles.Clear();
        }
        for (int i = 1; i < costFieldEditRequests.Count; i++)
        {
            for (int j = 0; j < costFieldEditRequests[i].Length; j++)
            {
                editHandles.Add(costFieldEditRequests[i][j].Schedule(lastHandle));
            }
            lastHandle = JobHandle.CombineDependencies(editHandles);
            editHandles.Clear();
        }

        if (FlowFieldUtilities.DebugMode) { lastHandle.Complete(); }

        return lastHandle;
    }
    public void AddMovementDataCalculationHandle(JobHandle dependency)
    {
        AgentMovementDataCalculationJob movDataJob = _dirCalculator.CalculateDirections(out TransformAccessArray transformsToSchedule);
        _movDataCalcHandle.Add(movDataJob.Schedule(transformsToSchedule,dependency));

        if (FlowFieldUtilities.DebugMode) { _movDataCalcHandle[_movDataCalcHandle.Count - 1].Complete(); }
    }
    public void AddCollisionCalculationJob()
    {
        CollisionCalculationJob collisionJob = new CollisionCalculationJob()
        {
            TileSize = _pathfindingManager.TileSize,
            FieldColAmount = _pathfindingManager.ColumnAmount,
            FieldRowAmount = _pathfindingManager.RowAmount,
            VertexSequence = _pathfindingManager.FieldProducer.GetVertexSequence(),
            EdgeDirections = _pathfindingManager.FieldProducer.GetEdgeDirections(),
            TileToWallObject = _pathfindingManager.FieldProducer.GetTileToWallObject(),
            WallObjectList = _pathfindingManager.FieldProducer.GetWallObjectList(),
            AgentMovementData = _dirCalculator._agentMovementDataList,
        };
        JobHandle collisionHandle = collisionJob.Schedule(collisionJob.AgentMovementData.Length,512 ,_movDataCalcHandle[0]);
        collisionHandle.Complete();
    }
    public void AddPortalTraversalHandles(List<PortalTraversalJobPack> portalTravJobs, JobHandle dependency)
    {
        for (int i = 0; i < portalTravJobs.Count; i++)
        {
            if (portalTravJobs[i].Path == null) { continue; }
            _porTravHandles.Add(portalTravJobs[i].Schedule(dependency));
        }
        portalTravJobs.Clear();

        if (FlowFieldUtilities.DebugMode)
        {
            for(int i = 0; i < _porTravHandles.Count; i++)
            {
                _porTravHandles[i].Handle.Complete();
            }
        }
    }
    public void SetPortalAdditionTraversalHandles()
    {
        _pathfindingManager.PathProducer.SetPortalAdditionTraversalHandles(_dirCalculator._agentMovementDataList, _porAddTravHandles, _movDataCalcHandle[0]);

        if (FlowFieldUtilities.DebugMode)
        {
            for(int i = 0; i <_porAddTravHandles.Count; i++)
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

                if(handle.Path.IntegrationStartIndicies.Length != 0)
                {
                    JobHandle additionHandle = _pathfindingManager.PathProducer.SchedulePathAdditionJob(handle.Path);
                    _pathAdditionHandles.Add(additionHandle);
                }
            }
        }

        if (FlowFieldUtilities.DebugMode)
        {
            for(int i = 0; i < _pathProdCalcHandles.Count; i++)
            {
                _pathProdCalcHandles[i].Handle.Complete();
            }
            JobHandle.CompleteAll(_pathAdditionHandles);
        }
    }
    public void ForceCompleteAll()
    {
        //FORCE COMPLETE MOVEMENT DATA CALCULATION
        if(_movDataCalcHandle.Count == 1)
        {
            _movDataCalcHandle[0].Complete();
            _movDataCalcHandle.Clear();
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
        _dirCalculator.SendDirections();
    }
}