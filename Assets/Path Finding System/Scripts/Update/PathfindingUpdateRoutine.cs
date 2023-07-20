using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class PathfindingUpdateRoutine
{
    PathfindingManager _pathfindingManager;
    RoutineSchedulingTree _schedulingTree;

    List<CostFieldEditJob[]> _costEditRequests;
    List<PortalTraversalJobPack> _portalTravJobs;

    public PathfindingUpdateRoutine(PathfindingManager pathfindingManager, PathProducer pathProducer)
    {
        _pathfindingManager = pathfindingManager;
        _schedulingTree = new RoutineSchedulingTree(pathfindingManager);

        _costEditRequests = new List<CostFieldEditJob[]>();
        _portalTravJobs = new List<PortalTraversalJobPack>();
    }
    public void RoutineUpdate(float deltaTime)
    {
        //FORCE COMPLETE JOBS FROM PREVIOUS UPDATE
        _schedulingTree.ForceCompleteAll();

        _pathfindingManager.PathProducer.Update();

        //SCHEDULE NEW JOBS
        JobHandle costEditHandle = _schedulingTree.ScheduleCostEditRequests(_costEditRequests);
        _costEditRequests.Clear();
        _schedulingTree.AddMovementDataCalculationHandle(costEditHandle);
        _schedulingTree.SetPortalAdditionTraversalHandles();
        _schedulingTree.AddPortalTraversalHandles(_portalTravJobs, costEditHandle);
        _portalTravJobs.Clear();
    }
    public void IntermediateLateUpdate()
    {
        _schedulingTree.TryCompletePredecessorJobs();
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
