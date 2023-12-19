using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

public class PathfindingUpdateRoutine
{
    PathfindingManager _pathfindingManager;
    RoutineScheduler _scheduler;

    List<CostFieldEditJob[]> _costEditRequests;
    NativeList<CostEditRequest> _requestedCostEditBoundaries;
    List<PortalTraversalJobPack> _portalTravJobs;
    List<FlowFieldAgent> _agentAddRequest;

    NativeList<PathRequest> PathRequests;
    public PathfindingUpdateRoutine(PathfindingManager pathfindingManager, PathContainer pathProducer)
    {
        _pathfindingManager = pathfindingManager;

        _costEditRequests = new List<CostFieldEditJob[]>();
        _portalTravJobs = new List<PortalTraversalJobPack>();
        _agentAddRequest = new List<FlowFieldAgent>();
        PathRequests = new NativeList<PathRequest>(Allocator.Persistent);
        _scheduler = new RoutineScheduler(pathfindingManager);
        _requestedCostEditBoundaries = new NativeList<CostEditRequest>(Allocator.Persistent);

    }
    public void RoutineUpdate(float deltaTime)
    {
        //FORCE COMPLETE JOBS FROM PREVIOUS UPDATE
        _scheduler.ForceCompleteAll();
        _pathfindingManager.PathContainer.Update();
        //ADD NEW AGENTS
        for (int i = 0; i < _agentAddRequest.Count; i++)
        {
            _pathfindingManager.AgentDataContainer.Subscribe(_agentAddRequest[i]);
        }
        _agentAddRequest.Clear();

        //SCHEDULE NEW JOBS
        _scheduler.Schedule(_requestedCostEditBoundaries.AsArray().AsReadOnly(), PathRequests);

        _requestedCostEditBoundaries.Clear();
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
        int2 b1 = new int2(startingPoint.x, startingPoint.y);
        int2 b2 = new int2(endPoint.x, endPoint.y);
        _requestedCostEditBoundaries.Add(new CostEditRequest(b1, b2, newCost));
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
    public void RequestPath(List<FlowFieldAgent> agents, FlowFieldAgent targetAgent)
    {
        int newPathIndex = PathRequests.Length;
        int targetAgentIndex = targetAgent.AgentDataIndex;
        PathRequest request = new PathRequest(targetAgentIndex);
        PathRequests.Add(request);
        _pathfindingManager.AgentDataContainer.SetRequestedPathIndiciesOf(agents, newPathIndex);
    }
}
public struct CostEditRequest
{
    public int2 BoundaryBotLeft;
    public int2 BoundaryTopRight;
    public byte NewCost;

    public CostEditRequest(int2 bound1, int2 bound2, byte newCost)
    {
        int lowerRow = bound1.y < bound2.y ? bound1.y : bound2.y;
        int upperRow = bound1.y > bound2.y ? bound1.y : bound2.y;
        int leftmostCol = bound1.x < bound2.x ? bound1.x : bound2.x;
        int rightmostCol = bound1.x > bound2.x ? bound1.x : bound2.x;

        BoundaryBotLeft = new int2(leftmostCol, lowerRow);
        BoundaryTopRight = new int2(rightmostCol, upperRow);
        NewCost = newCost;
    }
}