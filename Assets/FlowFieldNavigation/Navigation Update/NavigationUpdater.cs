using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

public class NavigationUpdater
{
    PathfindingManager _pathfindingManager;
    RoutineScheduler _scheduler;
    RequestAccumulator _requestAccumulator;
    public NavigationUpdater(PathfindingManager pathfindingManager, RequestAccumulator requestAccumulator)
    {
        _pathfindingManager = pathfindingManager;
        _requestAccumulator = requestAccumulator;
        _scheduler = new RoutineScheduler(pathfindingManager);
    }
    public void RoutineFixedUpdate()
    {
        _scheduler.ForceCompleteAll();
        _pathfindingManager.PathContainer.Update();
        List<FlowFieldAgent> agentAddRequest = _requestAccumulator.AgentAddRequest;
        NativeList<PathRequest> pathRequests = _requestAccumulator.PathRequests;
        NativeList<CostEdit> costEditRequests = _requestAccumulator.CostEditRequests;

        for (int i = 0; i < agentAddRequest.Count; i++)
        {
            _pathfindingManager.AgentDataContainer.Subscribe(agentAddRequest[i]);
        }

        _scheduler.Schedule(pathRequests, costEditRequests.AsArray().AsReadOnly());

        pathRequests.Clear();
        costEditRequests.Clear();
        agentAddRequest.Clear();
    }
    public RoutineScheduler GetRoutineScheduler()
    {
        return _scheduler;
    }
    public void IntermediateUpdate()
    {
        _scheduler.TryCompletePredecessorJobs();
    }
}