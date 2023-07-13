using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.Jobs;
using static UnityEngine.GraphicsBuffer;

public class PathfindingUpdateRoutine
{
    PathfindingManager _pathfindingManager;
    AgentDirectionCalculator _dirCalculator;

    List<CostFieldEditJob[]> costEditRequests = new List<CostFieldEditJob[]>();
    List<PortalTraversalJobPack> travJobPackList = new List<PortalTraversalJobPack>();

    List<PortalTraversalHandle> _poralTraversalHandles;

    NativeList<JobHandle> _routineHandles;
    List<FlowFieldHandle> _flowFieldHandleList;
    public PathfindingUpdateRoutine(PathfindingManager pathfindingManager, PathProducer pathProducer)
    {
        _pathfindingManager = pathfindingManager;
        _dirCalculator = new AgentDirectionCalculator(_pathfindingManager.AgentDataContainer, _pathfindingManager);
        _routineHandles = new NativeList<JobHandle>(Allocator.Persistent);
        _poralTraversalHandles = new List<PortalTraversalHandle>();
        _flowFieldHandleList = new List<FlowFieldHandle>();
    }
    public void RoutineUpdate(float deltaTime)
    {
        //COMPLETE ALL SCHEDULED JOBS
        JobHandle.CompleteAll(_routineHandles);
        _dirCalculator.SendDirections();
        _routineHandles.Clear();
        for(int i = 0; i < _flowFieldHandleList.Count; i++)
        {
            FlowFieldHandle handle = _flowFieldHandleList[i];
            handle.Handle.Complete();
            handle.path.IsCalculated = true;
        }
        _flowFieldHandleList.Clear();

        //SCHEDULE COST EDITS
        int lastEditIndex = costEditRequests.Count - 1;
        NativeList<JobHandle> editHandles = new NativeList<JobHandle>(Allocator.Temp);
        for (int i = 0; i < costEditRequests.Count; i++)
        {
            if (_routineHandles.Length == 0)
            {
                for (int j = 0; j < costEditRequests[i].Length; j++)
                {
                    editHandles.Add(costEditRequests[i][j].Schedule());
                }
            }
            else
            {
                for (int j = 0; j < costEditRequests[i].Length; j++)
                {
                    editHandles.Add(costEditRequests[i][j].Schedule(_routineHandles[_routineHandles.Length - 1]));
                }
            }
            JobHandle combinedHandle = JobHandle.CombineDependencies(editHandles);
            _routineHandles.Add(combinedHandle);
            editHandles.Clear();
        }
        costEditRequests.Clear();

        //SCHEDULE MOVEMENT DATA CALCULATION
        AgentMovementDataCalculationJob movDataJob = _dirCalculator.CalculateDirections(out TransformAccessArray transformsToSchedule);
        if (lastEditIndex == -1) { _routineHandles.Add(movDataJob.Schedule(transformsToSchedule)); }
        else { _routineHandles.Add(movDataJob.Schedule(transformsToSchedule, _routineHandles[lastEditIndex])); }

        //SCHEDULE PORTAL TRAVERSAL JOBS
        for(int i = 0; i < travJobPackList.Count; i++)
        {
            if (travJobPackList[i].Path == null) { continue; }
            else if(lastEditIndex == -1) { _poralTraversalHandles.Add(travJobPackList[i].Schedule()); }
            else { _poralTraversalHandles.Add(travJobPackList[i].Schedule(_routineHandles[lastEditIndex])); }
        }
        travJobPackList.Clear();
    }
    public void IntermediateLateUpdate()
    {
        for(int i = 0; i < _poralTraversalHandles.Count; i++)
        {
            PortalTraversalHandle handle = _poralTraversalHandles[i];
            handle.Complete();
            _flowFieldHandleList.Add(_pathfindingManager.PathProducer.ScheduleFlowFieldJob(handle.path));
        }
        _poralTraversalHandles.Clear();
    }
    public void RequestCostEdit(int2 startingPoint, int2 endPoint, byte newCost)
    {
        Index2 b1 = new Index2(startingPoint.y, startingPoint.x);
        Index2 b2 = new Index2(endPoint.y, endPoint.x);
        CostFieldEditJob[] costEditJobs = _pathfindingManager.CostFieldProducer.GetEditJobs(new BoundaryData(b1, b2), newCost);
        costEditRequests.Add(costEditJobs);
    }
    public Path RequestPath(NativeArray<Vector3> sources, Vector2 destination, int offset)
    {
        PortalTraversalJobPack portalTravJobPack = _pathfindingManager.PathProducer.GetPortalTraversalJobPack(sources, destination, offset);
        if(portalTravJobPack.Path != null)
        {
            travJobPackList.Add(portalTravJobPack);
        }
        return portalTravJobPack.Path;
    }
}
