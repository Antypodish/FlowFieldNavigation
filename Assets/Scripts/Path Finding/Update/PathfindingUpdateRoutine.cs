using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
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
    RoutineSchedulingTree _schedulingTree;

    List<CostFieldEditJob[]> _costEditRequests;
    List<PortalTraversalJobPack> _portalTravJobs;

    public PathfindingUpdateRoutine(PathfindingManager pathfindingManager, PathProducer pathProducer)
    {
        _pathfindingManager = pathfindingManager;
        _schedulingTree = new RoutineSchedulingTree(pathfindingManager);

        _dirCalculator = new AgentDirectionCalculator(_pathfindingManager.AgentDataContainer, _pathfindingManager);
        _costEditRequests = new List<CostFieldEditJob[]>();
        _portalTravJobs = new List<PortalTraversalJobPack>();
    }
    public void RoutineUpdate(float deltaTime)
    {
        //COMPLETE ALL SCHEDULED JOBS
        _schedulingTree.ForceCompleteAll(_dirCalculator);

        //SCHEDULE COST EDITS
        _schedulingTree.AddCostEditHandles(_costEditRequests);
        _costEditRequests.Clear();

        //SCHEDULE MOVEMENT DATA CALCULATION
        AgentMovementDataCalculationJob movDataJob = _dirCalculator.CalculateDirections(out TransformAccessArray transformsToSchedule);
        _schedulingTree.AddMovementDataCalculationHandle(ref movDataJob, transformsToSchedule);

        //SCHEDULE PORTAL TRAVERSAL JOBS
        _schedulingTree.AddPortalTraversalHandles(_portalTravJobs);
        _portalTravJobs.Clear();
    }
    public void IntermediateLateUpdate()
    {
        _schedulingTree.TryCompletePredecessorJobs(ref _dirCalculator._agentMovementDataList);
    }
    public void RequestCostEdit(int2 startingPoint, int2 endPoint, byte newCost)
    {
        Index2 b1 = new Index2(startingPoint.y, startingPoint.x);
        Index2 b2 = new Index2(endPoint.y, endPoint.x);
        CostFieldEditJob[] costEditJobs = _pathfindingManager.CostFieldProducer.GetEditJobs(new BoundaryData(b1, b2), newCost);
        _costEditRequests.Add(costEditJobs);
    }
    public Path RequestPath(NativeArray<Vector3> sources, Vector2 destination, int offset)
    {
        PortalTraversalJobPack portalTravJobPack = _pathfindingManager.PathProducer.GetPortalTraversalJobPack(sources, destination, offset);
        if(portalTravJobPack.Path != null)
        {
            _portalTravJobs.Add(portalTravJobPack);
        }
        return portalTravJobPack.Path;
    }
}
