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


    List<JobHandle> _costEditHandles;
    List<FlowFieldHandle> _pathProdCalcHandles;

    //PREDECESSOR HANDLES (Handles having successors wich depens on the output of these jobs)
    List<PortalTraversalHandle> _porTravHandles;
    List<PortalAdditionTraversalHandle> _porAddTravHandles;
    List<JobHandle> _movDataCalcHandle;

    public RoutineSchedulingTree(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;

        _costEditHandles = new List<JobHandle>();
        _porTravHandles = new List<PortalTraversalHandle>();
        _porAddTravHandles = new List<PortalAdditionTraversalHandle>();
        _movDataCalcHandle = new List<JobHandle>();
        _pathProdCalcHandles = new List<FlowFieldHandle>();
    }

    public void AddCostEditHandles(List<CostFieldEditJob[]> costFieldEditRequests)
    {
        NativeList<JobHandle> editHandles = new NativeList<JobHandle>(Allocator.Temp);
        for (int i = 0; i < costFieldEditRequests.Count; i++)
        {
            if (_costEditHandles.Count == 0)
            {
                for (int j = 0; j < costFieldEditRequests[i].Length; j++)
                {
                    editHandles.Add(costFieldEditRequests[i][j].Schedule());
                }
            }
            else
            {
                for (int j = 0; j < costFieldEditRequests[i].Length; j++)
                {
                    editHandles.Add(costFieldEditRequests[i][j].Schedule(_costEditHandles[_costEditHandles.Count - 1]));
                }
            }
            JobHandle combinedHandle = JobHandle.CombineDependencies(editHandles);
            _costEditHandles.Add(combinedHandle);
            editHandles.Clear();
        }
    }
    public void AddMovementDataCalculationHandle(ref AgentMovementDataCalculationJob movDataCalcJob, TransformAccessArray transformsToSchedule)
    {
        int lastEditIndex = _costEditHandles.Count - 1;
        if (lastEditIndex == -1) { _movDataCalcHandle.Add(movDataCalcJob.Schedule(transformsToSchedule)); }
        else { _movDataCalcHandle.Add(movDataCalcJob.Schedule(transformsToSchedule, _costEditHandles[lastEditIndex])); }
    }
    public void AddPortalTraversalHandles(List<PortalTraversalJobPack> portalTravJobs)
    {
        int lastEditIndex = _costEditHandles.Count - 1;
        for (int i = 0; i < portalTravJobs.Count; i++)
        {
            if (portalTravJobs[i].Path == null) { continue; }
            else if (lastEditIndex == -1) { _porTravHandles.Add(portalTravJobs[i].Schedule()); }
            else { _porTravHandles.Add(portalTravJobs[i].Schedule(_costEditHandles[lastEditIndex])); }
        }
        portalTravJobs.Clear();
    }
    public void TryCompletePredecessorJobs(ref NativeList<AgentMovementData> agentMovementDataList)
    {
        //HANDLE PORTAL TRAVERSALS
        for (int i = 0; i < _porTravHandles.Count; i++)
        {
            PortalTraversalHandle handle = _porTravHandles[i];
            handle.Complete();
            _pathProdCalcHandles.Add(_pathfindingManager.PathProducer.ScheduleFlowFieldJob(handle.path));
        }
        _porTravHandles.Clear();

        //HANDLE MOVEMENT DATA CALCULATION
        if (_movDataCalcHandle.Count == 1)
        {
            if (_movDataCalcHandle[0].IsCompleted)
            {
                _movDataCalcHandle[0].Complete();
                _pathfindingManager.PathProducer.SetPortalAdditionHandles(agentMovementDataList, _porAddTravHandles);
                _movDataCalcHandle.Clear();
            }
        }

        //HANDLE PORTAL ADD TRAVERSALS
        for (int i = _porAddTravHandles.Count - 1; i >= 0; i--)
        {
            PortalAdditionTraversalHandle handle = _porAddTravHandles[i];
            if (handle.Handle.IsCompleted)
            {
                handle.Handle.Complete();
            }
        }
    }

    public void ForceCompleteAll(AgentDirectionCalculator dirCalculator)
    {
        for(int i = 0; i < _costEditHandles.Count; i++)
        {
            _costEditHandles[i].Complete();
        }
        dirCalculator.SendDirections();
        _costEditHandles.Clear();
        for (int i = 0; i < _pathProdCalcHandles.Count; i++)
        {
            FlowFieldHandle handle = _pathProdCalcHandles[i];
            handle.Handle.Complete();
            handle.path.IsCalculated = true;
        }
        _pathProdCalcHandles.Clear();
    }
    void Reset()
    {
        _costEditHandles.Clear();
        _porAddTravHandles.Clear();
        _porTravHandles.Clear();
        _movDataCalcHandle.Clear();
        _pathProdCalcHandles.Clear();
    }
}