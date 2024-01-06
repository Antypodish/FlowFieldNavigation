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

    List<FlowFieldAgent> _agentAddRequest;

    NativeList<PathRequest> PathRequests;
    NativeList<CostEdit> _costEditRequests;
    public PathfindingUpdateRoutine(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
        _agentAddRequest = new List<FlowFieldAgent>();
        PathRequests = new NativeList<PathRequest>(Allocator.Persistent);
        _scheduler = new RoutineScheduler(pathfindingManager);
        _costEditRequests = new NativeList<CostEdit>(Allocator.Persistent);
    }
    public void RoutineUpdate(float deltaTime)
    {
        //FORCE COMPLETE JOBS FROM PREVIOUS UPDATE
        _pathfindingManager.PathContainer.Update();
        //ADD NEW AGENTS
        for (int i = 0; i < _agentAddRequest.Count; i++)
        {
            _pathfindingManager.AgentDataContainer.Subscribe(_agentAddRequest[i]);
        }
        _agentAddRequest.Clear();

        //SCHEDULE NEW JOBS
        _scheduler.Schedule(PathRequests, _costEditRequests.AsArray().AsReadOnly());

        PathRequests.Clear();
        _costEditRequests.Clear();
        _scheduler.ForceCompleteAll();
    }
    public RoutineScheduler GetRoutineScheduler()
    {
        return _scheduler;
    }
    public void IntermediateLateUpdate()
    {
        _scheduler.TryCompletePredecessorJobs();
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

    public void HandleObstacleRequest(NativeArray<ObstacleRequest> obstacleRequests, NativeList<int> outputListToAddObstacleIndicies)
    {
        ObstacleRequestToCostEdit obstacleToEdit = new ObstacleRequestToCostEdit()
        {
            TileSize = FlowFieldUtilities.TileSize,
            FieldColAmount = FlowFieldUtilities.FieldColAmount,
            FieldRowAmount = FlowFieldUtilities.FieldRowAmount,
            FieldMaxXExcluding = FlowFieldUtilities.FieldMaxXExcluding,
            FieldMaxYExcluding = FlowFieldUtilities.FieldMaxYExcluding,
            FieldMinXIncluding = FlowFieldUtilities.FieldMinXIncluding,
            FieldMinYIncluding = FlowFieldUtilities.FieldMinYIncluding,
            CostEditOutput = _costEditRequests,
            ObstacleRequests = obstacleRequests,
            NewObstacleKeyListToAdd = outputListToAddObstacleIndicies,
            ObstacleList = _pathfindingManager.ObstacleContainer.ObstacleList,
            RemovedObstacleIndexList = _pathfindingManager.ObstacleContainer.RemovedIndexList,
        };
        obstacleToEdit.Schedule().Complete();
    }
    public void HandleObstacleRemovalRequest(NativeArray<int>.ReadOnly obstacleIndiciesToRemove)
    {
        ObstacleRemovalRequestToCostEdit obstacleToEdit = new ObstacleRemovalRequestToCostEdit()
        {
            CostEditOutput = _costEditRequests,
            ObstacleRemovalIndicies = obstacleIndiciesToRemove,
            ObstacleList = _pathfindingManager.ObstacleContainer.ObstacleList,
            RemovedObstacleIndexList = _pathfindingManager.ObstacleContainer.RemovedIndexList,
        };
        obstacleToEdit.Schedule().Complete();
    }
}