using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

internal class NavigationUpdater
{
    PathfindingManager _pathfindingManager;
    RoutineScheduler _scheduler;
    RequestAccumulator _requestAccumulator;

    internal NavigationUpdater(PathfindingManager pathfindingManager, RequestAccumulator requestAccumulator)
    {
        _pathfindingManager = pathfindingManager;
        _requestAccumulator = requestAccumulator;
        _scheduler = new RoutineScheduler(pathfindingManager);
    }
    internal void RoutineFixedUpdate()
    {
        _scheduler.ForceCompleteAll();
        List<FlowFieldAgent> agentAddRequest = _requestAccumulator.AgentAddRequest;
        List<FlowFieldAgent> agentRemovalRequests = _requestAccumulator.AgentRemovalRequests;
        NativeList<PathRequest> pathRequests = _requestAccumulator.PathRequests;
        NativeList<CostEdit> costEditRequests = _requestAccumulator.CostEditRequests;

        for (int i = 0; i < agentAddRequest.Count; i++)
        {
            _pathfindingManager.AgentDataContainer.Subscribe(agentAddRequest[i]);
        }
        _pathfindingManager.AgentRemovingSystem.RemoveAgents(agentRemovalRequests);
        _pathfindingManager.PathDataContainer.Update();

        _scheduler.Schedule(pathRequests, costEditRequests.AsArray().AsReadOnly());

        pathRequests.Clear();
        costEditRequests.Clear();
        agentAddRequest.Clear();
        agentRemovalRequests.Clear();
    }
    internal uint GetFieldState()
    {
        return _scheduler.FieldState;
    }
    internal void IntermediateUpdate()
    {
        _scheduler.TryCompletePredecessorJobs();
    }
    internal void DisposeAll()
    {
        _scheduler.DisposeAll();
    }
}