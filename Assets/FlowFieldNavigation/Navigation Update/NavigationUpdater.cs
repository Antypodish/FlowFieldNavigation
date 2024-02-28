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
        RequestAccumulator _requestAccumulator;
        PathfindingManager _pathfindingManager;
        MovementManager _movementManager;
        CostFieldEditManager _fieldEditManager;

        float _timePassedSinceLastUpdate;
        const float _updateFrequency = 0.02f;
        const int _smallUpdateCountForTriggeringBigUpdate = 3;
        int _smallUpdateCount = 1;
        internal NavigationUpdater(FlowFieldNavigationManager navigationManager, RequestAccumulator requestAccumulator)
        {
            _navigationManager = navigationManager;
            _requestAccumulator = requestAccumulator;
            _pathfindingManager = navigationManager.PathfindingManager;
            _movementManager = navigationManager.MovementManager;
            _fieldEditManager = navigationManager.FieldEditManager;
        }
        internal void IntermediateUpdate()
        {
            _fieldEditManager.TryComplete();
            _pathfindingManager.TryComplete();
        }
        internal void RoutineFixedUpdate()
        {
            int updateCount = SetTimerAndGetUpdateCount();
            for (int j = 0; j < updateCount; j++)
            {
                if(_smallUpdateCount % _smallUpdateCountForTriggeringBigUpdate == 0)
                {
                    BigUpdate();
                    _smallUpdateCount = 1;
                }
                else
                {
                    SmallUpdate();
                    _smallUpdateCount++;
                }
            }
        }
        void SmallUpdate()
        {
            _movementManager.ForceCompleteRoutine();
            _movementManager.SendRoutineResults(_updateFrequency);
            _movementManager.ScheduleRoutine(_updateFrequency, _fieldEditManager.GetCurrentCostFieldEditHandle());
        }
        void BigUpdate()
        {
            _fieldEditManager.ForceComplete();
            _movementManager.ForceCompleteRoutine();
            _pathfindingManager.ForceComplete();
            _movementManager.SendRoutineResults(_updateFrequency);
            _pathfindingManager.TransferNewPathsToCurPaths();
            List<FlowFieldAgent> agentAddRequest = _requestAccumulator.AgentAddRequest;
            NativeList<int> subReqAgentDataRefIndicies = _requestAccumulator.SubscriptionRequestedAgentDataReferanceIndicies;
            NativeList<int> agentReferanceIndiciesToRemove = _requestAccumulator.AgentReferanceIndiciesToRemove;
            NativeList<PathRequest> pathRequests = _requestAccumulator.PathRequests;
            NativeList<CostEdit> costEditRequests = _requestAccumulator.CostEditRequests;
            NativeList<int> agentsToHoldGround = _requestAccumulator.AgentIndiciesToSetHoldGround;
            NativeList<int> agentsToStop = _requestAccumulator.AgentIndiciesToStop;
            NativeList<SetSpeedReq> setSpeedRequests = _requestAccumulator.SetSpeedRequests;

            _navigationManager.AgentStatChangeSystem.SetAgentsHoldGround(agentsToHoldGround.AsArray());
            _navigationManager.AgentStatChangeSystem.SetAgentsStopped(agentsToStop.AsArray());
            _navigationManager.AgentStatChangeSystem.SetAgentSpeed(setSpeedRequests.AsArray());
            _navigationManager.AgentAdditionSystem.AddAgents(agentAddRequest, subReqAgentDataRefIndicies);
            _navigationManager.AgentRemovingSystem.RemoveAgents(agentReferanceIndiciesToRemove.AsArray());
            _navigationManager.PathDataContainer.Update();

            NativeArray<IslandFieldProcessor> islandFieldProcessors = _navigationManager.FieldDataContainer.GetAllIslandFieldProcessors(Allocator.Persistent);
            _fieldEditManager.Schedule(costEditRequests.AsArray().AsReadOnly());
            _pathfindingManager.ShcedulePathRequestEvalutaion(pathRequests.AsArray(), islandFieldProcessors, _fieldEditManager.EditedSectorBitArraysForEachField, _fieldEditManager.GetCurrentIslandFieldReconfigHandle());
            _movementManager.ScheduleRoutine(_updateFrequency, _fieldEditManager.GetCurrentCostFieldEditHandle());

            pathRequests.Clear();
            costEditRequests.Clear();
            agentAddRequest.Clear();
            subReqAgentDataRefIndicies.Clear();
            agentReferanceIndiciesToRemove.Clear();
            agentsToHoldGround.Clear();
            agentsToStop.Clear();
            setSpeedRequests.Clear();
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
        internal void DisposeAll()
        {
            _fieldEditManager.DisposeAll();
            _pathfindingManager.DisposeAll();
            _movementManager.DisposeAll();
        }
    }

}
