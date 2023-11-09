using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class PathfindingUpdateRoutine
{
    PathfindingManager _pathfindingManager;
    RoutineScheduler _scheduler;

    List<CostFieldEditJob[]> _costEditRequests;
    List<PortalTraversalJobPack> _portalTravJobs;
    List<FlowFieldAgent> _agentAddRequest;

    NativeList<PathRequest> PathRequests;
    public PathfindingUpdateRoutine(PathfindingManager pathfindingManager, PathProducer pathProducer)
    {
        _pathfindingManager = pathfindingManager;

        _costEditRequests = new List<CostFieldEditJob[]>();
        _portalTravJobs = new List<PortalTraversalJobPack>();
        _agentAddRequest = new List<FlowFieldAgent>();
        PathRequests = new NativeList<PathRequest>(Allocator.Persistent);
        _scheduler = new RoutineScheduler(pathfindingManager);

    }
    public void RoutineUpdate(float deltaTime)
    {
        //FORCE COMPLETE JOBS FROM PREVIOUS UPDATE
        _scheduler.ForceCompleteAll();
        _pathfindingManager.PathProducer.Update();
        //ADD NEW AGENTS
        for (int i = 0; i < _agentAddRequest.Count; i++)
        {
            _pathfindingManager.AgentDataContainer.Subscribe(_agentAddRequest[i]);
        }
        _agentAddRequest.Clear();

        //SCHEDULE NEW JOBS
        _scheduler.Schedule(_costEditRequests, PathRequests);

        PathRequests.Clear();
        _costEditRequests.Clear();
        _portalTravJobs.Clear();

    }
    public RoutineScheduler GetRoutineScheduler()
    {
        return _scheduler;
    }
    public void IntermediateLateUpdate()
    {
        _scheduler.TryCompletePredecessorJobs();
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
    public void RequestPath(List<FlowFieldAgent> agents, Vector3 target)
    {
        int newPathIndex = PathRequests.Length;
        float2 target2d = new float2(target.x, target.z);
        PathRequests.Add(new PathRequest(target2d));
        _pathfindingManager.AgentDataContainer.SetRequestedPathIndiciesOf(agents, newPathIndex);
    }
}
