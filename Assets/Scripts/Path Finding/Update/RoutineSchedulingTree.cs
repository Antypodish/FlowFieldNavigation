using System.Collections.Generic;
using TMPro.EditorUtilities;
using Unity.Collections;
using Unity.Jobs;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine.Jobs;

public class RoutineSchedulingTree
{
    PathfindingManager _pathfindingManager;
    AgentDirectionCalculator _dirCalculator;

    List<FlowFieldHandle> _pathProdCalcHandles;
    NativeList<JobHandle> _pathAdditionHandles;

    //PREDECESSOR HANDLES (Handles having successors wich depens on the output of these jobs)
    List<PortalTraversalHandle> _porTravHandles;
    List<PortalAdditionTraversalHandle> _pathAddTravHandles;
    List<JobHandle> _movDataCalcHandle;

    public RoutineSchedulingTree(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
        _dirCalculator = new AgentDirectionCalculator(pathfindingManager.AgentDataContainer, pathfindingManager);

        _porTravHandles = new List<PortalTraversalHandle>();
        _pathAddTravHandles = new List<PortalAdditionTraversalHandle>();
        _movDataCalcHandle = new List<JobHandle>();
        _pathProdCalcHandles = new List<FlowFieldHandle>();
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
        return lastHandle;
    }
    public void AddMovementDataCalculationHandle(JobHandle dependency)
    {
        AgentMovementDataCalculationJob movDataJob = _dirCalculator.CalculateDirections(out TransformAccessArray transformsToSchedule);
        _movDataCalcHandle.Add(movDataJob.Schedule(transformsToSchedule,dependency));
    }
    public void AddPortalTraversalHandles(List<PortalTraversalJobPack> portalTravJobs, JobHandle dependency)
    {
        for (int i = 0; i < portalTravJobs.Count; i++)
        {
            if (portalTravJobs[i].Path == null) { continue; }
            _porTravHandles.Add(portalTravJobs[i].Schedule(dependency));
        }
        portalTravJobs.Clear();
    }
    public void TryCompletePredecessorJobs()
    {
        //HANDLE PORTAL TRAVERSALS
        for (int i = 0; i < _porTravHandles.Count; i++)
        {
            PortalTraversalHandle handle = _porTravHandles[i];
            handle.Complete();
            _pathProdCalcHandles.Add(_pathfindingManager.PathProducer.SchedulePathProductionJob(handle.path));
        }
        _porTravHandles.Clear();

        //HANDLE MOVEMENT DATA CALCULATION
        if (_movDataCalcHandle.Count == 1)
        {
            if (_movDataCalcHandle[0].IsCompleted)
            {
                _movDataCalcHandle[0].Complete();
                _pathfindingManager.PathProducer.SetPortalAdditionTraversalHandles(_dirCalculator._agentMovementDataList, _pathAddTravHandles);
                _movDataCalcHandle.Clear();
            }
        }

        //HANDLE PORTAL ADD TRAVERSALS
        for (int i = _pathAddTravHandles.Count - 1; i >= 0; i--)
        {
            PortalAdditionTraversalHandle handle = _pathAddTravHandles[i];
            if (handle.Handle.IsCompleted)
            {
                handle.Handle.Complete();
                JobHandle additionHandle = _pathfindingManager.PathProducer.SchedulePathAdditionJob(handle.Path);
                _pathAdditionHandles.Add(additionHandle);
                _pathAddTravHandles.RemoveAtSwapBack(i);
            }
        }
    }
    public void ForceCompleteAll()
    {
        //FORCE COMPLETE MOVEMENT DATA CALCULATION
        if(_movDataCalcHandle.Count == 1)
        {
            _movDataCalcHandle[0].Complete();
            _pathfindingManager.PathProducer.SetPortalAdditionTraversalHandles(_dirCalculator._agentMovementDataList, _pathAddTravHandles);
            _movDataCalcHandle.Clear();
        }

        //FOCE COMTPLETE PATH PRODUCTION TRAVERSALS
        for (int i = 0; i < _porTravHandles.Count; i++)
        {
            PortalTraversalHandle handle = _porTravHandles[i];
            handle.Complete();
            _pathProdCalcHandles.Add(_pathfindingManager.PathProducer.SchedulePathProductionJob(handle.path));
        }
        _porTravHandles.Clear();

        //FORCE COMTPLETE PATH PRODUCTIONS
        for (int i = 0; i < _pathProdCalcHandles.Count; i++)
        {
            FlowFieldHandle handle = _pathProdCalcHandles[i];
            handle.Handle.Complete();
            handle.path.IsCalculated = true;
        }
        _pathProdCalcHandles.Clear();

        //FORCE COMPLETE PATH ADDITION TRAVERSALS
        for (int i = _pathAddTravHandles.Count - 1; i >= 0; i--)
        {
            PortalAdditionTraversalHandle handle = _pathAddTravHandles[i];
            handle.Handle.Complete();
            JobHandle additionHandle = _pathfindingManager.PathProducer.SchedulePathAdditionJob(handle.Path);
            _pathAdditionHandles.Add(additionHandle);
        }
        _pathAddTravHandles.Clear();

        //FORCE COMPLETE PATH ADDITIONS
        JobHandle.CompleteAll(_pathAdditionHandles);
        _pathAdditionHandles.Clear();

        //SEND DIRECTIONS
        _dirCalculator.SendDirections();
    }
}