using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using UnityEngine;

public class FlowFieldNavigationInterface
{
    PathfindingManager _pathfindingManager;

    public FlowFieldNavigationInterface(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
    }

    public void StartSimulation(SimulationStartParameters startParameters)
    {
        if (_pathfindingManager.SimulationStarted)
        {
            UnityEngine.Debug.Log("Request declined. Simulation is already started.");
            return;
        }
        _pathfindingManager.StartSimulation(startParameters);
    }
    public void SetDestination(List<FlowFieldAgent> agents, Vector3 target)
    {
        if (!_pathfindingManager.SimulationStarted) { return; }
        if (agents.Count == 0) { UnityEngine.Debug.Log("Agent list passed is empty"); return; }
        _pathfindingManager.RequestAccumulator.RequestPath(agents, target);
    }
    public void SetDestination(List<FlowFieldAgent> agents, FlowFieldAgent targetAgent)
    {
        if (!_pathfindingManager.SimulationStarted) { return; }
        if (agents.Count == 0) { return; }
        if(targetAgent.AgentDataIndex == -1) { SetDestination(agents, targetAgent.transform.position); return; }
        _pathfindingManager.RequestAccumulator.RequestPath(agents, targetAgent);
    }
    public void SetObstacle(NativeArray<ObstacleRequest> obstacleRequests, NativeList<int> outputListToAddObstacleIndicies)
    {
        if (!_pathfindingManager.SimulationStarted) { return; }
        _pathfindingManager.RequestAccumulator.HandleObstacleRequest(obstacleRequests, outputListToAddObstacleIndicies);
    }
    public void RemoveObstacle(NativeArray<int>.ReadOnly obstaclesToRemove)
    {
        if (!_pathfindingManager.SimulationStarted) { return; }
        _pathfindingManager.RequestAccumulator.HandleObstacleRemovalRequest(obstaclesToRemove);
    }
    public void RequestSubscription(FlowFieldAgent agent)
    {
        if(agent.AgentDataIndex != -1) { return; }
        _pathfindingManager.RequestAccumulator.RequestAgentAddition(agent);
    }
    public void RequestUnsubscription(FlowFieldAgent agent)
    {
        if(agent.AgentDataIndex == -1) { return; }
        _pathfindingManager.RequestAccumulator.RequestAgentRemoval(agent);
    }
    public int GetPathIndex(int agentIndex)
    {
        if (!_pathfindingManager.SimulationStarted) { return -1; }
        return _pathfindingManager.AgentDataContainer.AgentCurPathIndicies[agentIndex];
    }
    public List<FlowFieldAgent> GetAllAgents()
    {
        if (!_pathfindingManager.SimulationStarted) { return null; }
        return _pathfindingManager.AgentDataContainer.Agents;
    }
    public int GetAgentCount()
    {
        if (!_pathfindingManager.SimulationStarted) { return 0; }
        return _pathfindingManager.AgentDataContainer.Agents.Count;
    }

}
public struct SimulationStartParameters
{
    public float TileSize;
    public int RowCount;
    public int ColumCount;
    public Walkability[][] WalkabilityMatrix;
    public FlowFieldStaticObstacle[] StaticObstacles;
    public int MaxCostFieldOffset;
    public float BaseAgentSpatialGridSize;
    public Mesh[] Meshes;
    public Transform[] Transforms;
    public Vector2 FieldStartPositionXZ;
    public float VerticalVoxelSize;
    public float MaxSurfaceHeightDifference;
    public float MaxWalkableHeight;
}
public enum Walkability : byte
{
    Unwalkable,
    Walkable
}