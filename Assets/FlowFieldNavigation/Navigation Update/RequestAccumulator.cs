using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

internal class RequestAccumulator
{
    PathfindingManager _pathfindingManager;

    internal List<FlowFieldAgent> AgentAddRequest;
    internal NativeList<PathRequest> PathRequests;
    internal NativeList<CostEdit> CostEditRequests;
    internal RequestAccumulator(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
        AgentAddRequest = new List<FlowFieldAgent>();
        PathRequests = new NativeList<PathRequest>(Allocator.Persistent);
        CostEditRequests = new NativeList<CostEdit>(Allocator.Persistent);
    }
    internal void RequestAgentAddition(FlowFieldAgent agent)
    {
        AgentAddRequest.Add(agent);
    }
    internal void RequestPath(List<FlowFieldAgent> agents, Vector3 target)
    {
        int newPathIndex = PathRequests.Length;
        float2 target2d = new float2(target.x, target.z);
        PathRequests.Add(new PathRequest(target2d));
        _pathfindingManager.AgentDataContainer.SetRequestedPathIndiciesOf(agents, newPathIndex);
    }
    internal void RequestPath(List<FlowFieldAgent> agents, FlowFieldAgent targetAgent)
    {
        int newPathIndex = PathRequests.Length;
        int targetAgentIndex = targetAgent.AgentDataIndex;
        PathRequest request = new PathRequest(targetAgentIndex);
        PathRequests.Add(request);
        _pathfindingManager.AgentDataContainer.SetRequestedPathIndiciesOf(agents, newPathIndex);
    }

    internal void HandleObstacleRequest(NativeArray<ObstacleRequest> obstacleRequests, NativeList<int> outputListToAddObstacleIndicies)
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
            CostEditOutput = CostEditRequests,
            ObstacleRequests = obstacleRequests,
            NewObstacleKeyListToAdd = outputListToAddObstacleIndicies,
            ObstacleList = _pathfindingManager.FieldManager.ObstacleContainer.ObstacleList,
            RemovedObstacleIndexList = _pathfindingManager.FieldManager.ObstacleContainer.RemovedIndexList,
        };
        obstacleToEdit.Schedule().Complete();
    }
    internal void HandleObstacleRemovalRequest(NativeArray<int>.ReadOnly obstacleIndiciesToRemove)
    {
        ObstacleRemovalRequestToCostEdit obstacleToEdit = new ObstacleRemovalRequestToCostEdit()
        {
            CostEditOutput = CostEditRequests,
            ObstacleRemovalIndicies = obstacleIndiciesToRemove,
            ObstacleList = _pathfindingManager.FieldManager.ObstacleContainer.ObstacleList,
            RemovedObstacleIndexList = _pathfindingManager.FieldManager.ObstacleContainer.RemovedIndexList,
        };
        obstacleToEdit.Schedule().Complete();
    }
}
