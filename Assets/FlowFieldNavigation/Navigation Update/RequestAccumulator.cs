using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class RequestAccumulator
{
    PathfindingManager _pathfindingManager;

    public List<FlowFieldAgent> AgentAddRequest;
    public NativeList<PathRequest> PathRequests;
    internal NativeList<CostEdit> CostEditRequests;
    public RequestAccumulator(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
        AgentAddRequest = new List<FlowFieldAgent>();
        PathRequests = new NativeList<PathRequest>(Allocator.Persistent);
        CostEditRequests = new NativeList<CostEdit>(Allocator.Persistent);
    }
    public void RequestAgentAddition(FlowFieldAgent agent)
    {
        AgentAddRequest.Add(agent);
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
            CostEditOutput = CostEditRequests,
            ObstacleRequests = obstacleRequests,
            NewObstacleKeyListToAdd = outputListToAddObstacleIndicies,
            ObstacleList = _pathfindingManager.FieldManager.ObstacleContainer.ObstacleList,
            RemovedObstacleIndexList = _pathfindingManager.FieldManager.ObstacleContainer.RemovedIndexList,
        };
        obstacleToEdit.Schedule().Complete();
    }
    public void HandleObstacleRemovalRequest(NativeArray<int>.ReadOnly obstacleIndiciesToRemove)
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
