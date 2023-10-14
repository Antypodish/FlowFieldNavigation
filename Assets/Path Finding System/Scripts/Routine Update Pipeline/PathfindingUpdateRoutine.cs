using System.Collections.Generic;
using System.Diagnostics;
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
    List<FlowFieldAgent> _agentAddRequest;

    public PathfindingUpdateRoutine(PathfindingManager pathfindingManager, PathProducer pathProducer)
    {
        _pathfindingManager = pathfindingManager;
        _schedulingTree = new RoutineSchedulingTree(pathfindingManager);

        _costEditRequests = new List<CostFieldEditJob[]>();
        _portalTravJobs = new List<PortalTraversalJobPack>();
        _agentAddRequest = new List<FlowFieldAgent>();
    }
    public void RoutineUpdate(float deltaTime)
    {
        Stopwatch sw = new Stopwatch();
        //FORCE COMPLETE JOBS FROM PREVIOUS UPDATE
        _schedulingTree.ForceCompleteAll();

        _pathfindingManager.PathProducer.Update();

        //ADD NEW AGENTS
        for (int i = 0; i < _agentAddRequest.Count; i++)
        {
            _pathfindingManager.AgentDataContainer.Subscribe(_agentAddRequest[i]);
        }
        _agentAddRequest.Clear();

        //SCHEDULE NEW JOBS

        JobHandle costEditHandle = _schedulingTree.ScheduleCostEditRequests(_costEditRequests);

        _costEditRequests.Clear();

        _schedulingTree.AddMovementDataCalculationHandle(costEditHandle); 

        _schedulingTree.AddCollisionResolutionJob();
        sw.Start();

        _schedulingTree.AddLocalAvoidanceJob(); sw.Stop();

        _schedulingTree.AddCollisionCalculationJob(); 
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
        CostFieldEditJob[] costEditJobs = _pathfindingManager.FieldProducer.GetCostFieldEditJobs(new BoundaryData(b1, b2), newCost);
        _costEditRequests.Add(costEditJobs);
    }
    public void RequestAgentAddition(FlowFieldAgent agent)
    {
        _agentAddRequest.Add(agent);
    }
    public Path RequestPath(NativeArray<float2> sources, Vector2 destination, int offset)
    {
        
        PortalTraversalJobPack portalTravJobPack = _pathfindingManager.PathProducer.GetPortalTraversalJobPack(sources, destination, offset);
        if (portalTravJobPack.Path != null)
        {
            _portalTravJobs.Add(portalTravJobPack);
        }
        return portalTravJobPack.Path;
    }
}
