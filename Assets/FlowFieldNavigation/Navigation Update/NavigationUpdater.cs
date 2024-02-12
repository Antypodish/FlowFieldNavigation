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

    float _timePassedSinceLastUpdate;
    const float _updateFrequency = 0.02f;
    internal NavigationUpdater(PathfindingManager pathfindingManager, RequestAccumulator requestAccumulator)
    {
        _pathfindingManager = pathfindingManager;
        _requestAccumulator = requestAccumulator;
        _scheduler = new RoutineScheduler(pathfindingManager);
    }
    internal void RoutineUpdate()
    {
        int updateCount = SetTimerAndGetUpdateCount();
        for(int j = 0; j < updateCount; j++)
        {
            _scheduler.ForceCompleteAll(_updateFrequency);
            List<FlowFieldAgent> agentAddRequest = _requestAccumulator.AgentAddRequest;
            List<FlowFieldAgent> agentRemovalRequests = _requestAccumulator.AgentRemovalRequests;
            NativeList<PathRequest> pathRequests = _requestAccumulator.PathRequests;
            NativeList<CostEdit> costEditRequests = _requestAccumulator.CostEditRequests;
            NativeList<int> agentsToHoldGround = _requestAccumulator.AgentIndiciesToSetHoldGround;
            NativeList<int> agentsToStop = _requestAccumulator.AgentIndiciesToStop;
            for (int i = 0; i < agentAddRequest.Count; i++)
            {
                _pathfindingManager.AgentDataContainer.Subscribe(agentAddRequest[i]);
            }
            _pathfindingManager.AgentStatChangeSystem.SetAgentsHoldGround(agentsToHoldGround.AsArray());
            _pathfindingManager.AgentStatChangeSystem.SetAgentsStopped(agentsToStop.AsArray());
            _pathfindingManager.AgentRemovingSystem.RemoveAgents(agentRemovalRequests);
            _pathfindingManager.PathDataContainer.Update();

            _scheduler.Schedule(pathRequests, costEditRequests.AsArray().AsReadOnly());

            pathRequests.Clear();
            costEditRequests.Clear();
            agentAddRequest.Clear();
            agentRemovalRequests.Clear();
            agentsToHoldGround.Clear();
            agentsToStop.Clear();
        }
    }
    int SetTimerAndGetUpdateCount()
    {
        _timePassedSinceLastUpdate += Time.deltaTime;
        int updateCount = 0;
        if (_timePassedSinceLastUpdate >= _updateFrequency)
        {
            float amountOfUpdatesWithDecimal = _timePassedSinceLastUpdate / _updateFrequency;
            updateCount = (int)math.floor(amountOfUpdatesWithDecimal);
            _timePassedSinceLastUpdate = _timePassedSinceLastUpdate - (updateCount * _updateFrequency);
        }
        return updateCount;
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