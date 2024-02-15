using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

namespace FlowFieldNavigation
{
    internal class NavigationUpdater
    {
        FlowFieldNavigationManager _navigationManager;
        RoutineScheduler _scheduler;
        RequestAccumulator _requestAccumulator;

        float _timePassedSinceLastUpdate;
        const float _updateFrequency = 0.02f;
        internal NavigationUpdater(FlowFieldNavigationManager navigationManager, RequestAccumulator requestAccumulator)
        {
            _navigationManager = navigationManager;
            _requestAccumulator = requestAccumulator;
            _scheduler = new RoutineScheduler(navigationManager);
        }
        internal void RoutineUpdate()
        {
            int updateCount = SetTimerAndGetUpdateCount();
            for (int j = 0; j < updateCount; j++)
            {
                _scheduler.ForceCompleteAll(_updateFrequency);
                List<FlowFieldAgent> agentAddRequest = _requestAccumulator.AgentAddRequest;
                NativeList<int> agentIndiciesToRemove = _requestAccumulator.AgentIndiciesToRemove;
                NativeList<PathRequest> pathRequests = _requestAccumulator.PathRequests;
                NativeList<CostEdit> costEditRequests = _requestAccumulator.CostEditRequests;
                NativeList<int> agentsToHoldGround = _requestAccumulator.AgentIndiciesToSetHoldGround;
                NativeList<int> agentsToStop = _requestAccumulator.AgentIndiciesToStop;
                NativeList<SetSpeedReq> setSpeedRequests = _requestAccumulator.SetSpeedRequests;

                for (int i = 0; i < agentAddRequest.Count; i++)
                {
                    FlowFieldAgent agent = agentAddRequest[i];
                    if (agent == null) { continue; }
                    _navigationManager.AgentDataContainer.Subscribe(agent);
                }
                _navigationManager.AgentStatChangeSystem.SetAgentsHoldGround(agentsToHoldGround.AsArray());
                _navigationManager.AgentStatChangeSystem.SetAgentsStopped(agentsToStop.AsArray());
                _navigationManager.AgentStatChangeSystem.SetAgentSpeed(setSpeedRequests.AsArray());
                _navigationManager.AgentRemovingSystem.RemoveAgents(agentIndiciesToRemove.AsArray());
                _navigationManager.PathDataContainer.Update();

                _scheduler.Schedule(pathRequests, costEditRequests.AsArray().AsReadOnly());

                pathRequests.Clear();
                costEditRequests.Clear();
                agentAddRequest.Clear();
                agentIndiciesToRemove.Clear();
                agentsToHoldGround.Clear();
                agentsToStop.Clear();
                setSpeedRequests.Clear();
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

}
