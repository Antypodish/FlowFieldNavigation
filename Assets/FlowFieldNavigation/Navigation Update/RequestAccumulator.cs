using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

internal class RequestAccumulator
{
    PathfindingManager _pathfindingManager;

    internal List<FlowFieldAgent> AgentAddRequest;
    internal List<FlowFieldAgent> AgentRemovalRequests;
    internal NativeList<PathRequest> PathRequests;
    internal NativeList<CostEdit> CostEditRequests;
    internal NativeList<int> AgentIndiciesToSetHoldGround;
    internal NativeList<int> AgentIndiciesToStop;
    internal RequestAccumulator(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
        AgentAddRequest = new List<FlowFieldAgent>();
        AgentRemovalRequests = new List<FlowFieldAgent>();
        PathRequests = new NativeList<PathRequest>(Allocator.Persistent);
        CostEditRequests = new NativeList<CostEdit>(Allocator.Persistent);
        AgentIndiciesToSetHoldGround = new NativeList<int>(Allocator.Persistent);
        AgentIndiciesToStop = new NativeList<int>(Allocator.Persistent);
    }
    internal void RequestAgentAddition(FlowFieldAgent agent)
    {
        AgentAddRequest.Add(agent);
    }
    internal void RequestAgentRemoval(FlowFieldAgent agent)
    {
        AgentRemovalRequests.Add(agent);
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
    internal void RequestHoldGround(int agentIndex)
    {
        AgentIndiciesToSetHoldGround.Add(agentIndex);
    }
    internal void RequestStop(int agentIndex)
    {
        AgentIndiciesToStop.Add(agentIndex);
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
            FieldGridStartPos = FlowFieldUtilities.FieldGridStartPosition,
            CostEditOutput = CostEditRequests,
            ObstacleRequests = obstacleRequests,
            NewObstacleKeyListToAdd = outputListToAddObstacleIndicies,
            ObstacleList = _pathfindingManager.FieldDataContainer.ObstacleContainer.ObstacleList,
            RemovedObstacleIndexList = _pathfindingManager.FieldDataContainer.ObstacleContainer.RemovedIndexList,
        };
        obstacleToEdit.Schedule().Complete();
    }
    internal void HandleObstacleRemovalRequest(NativeArray<int>.ReadOnly obstacleIndiciesToRemove)
    {
        ObstacleRemovalRequestToCostEdit obstacleToEdit = new ObstacleRemovalRequestToCostEdit()
        {
            CostEditOutput = CostEditRequests,
            ObstacleRemovalIndicies = obstacleIndiciesToRemove,
            ObstacleList = _pathfindingManager.FieldDataContainer.ObstacleContainer.ObstacleList,
            RemovedObstacleIndexList = _pathfindingManager.FieldDataContainer.ObstacleContainer.RemovedIndexList,
        };
        obstacleToEdit.Schedule().Complete();
    }
    internal void DisposeAll()
    {
        AgentAddRequest.Clear();
        AgentAddRequest.TrimExcess();
        AgentAddRequest = null;
        PathRequests.Dispose();
        CostEditRequests.Dispose();
    }
}
