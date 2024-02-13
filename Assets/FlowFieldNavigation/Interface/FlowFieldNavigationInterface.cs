using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using UnityEngine;

public class FlowFieldNavigationInterface
{
    PathfindingManager _pathfindingManager;
    SimulationStartInputHandler _simStartInputHandler;
    public FlowFieldNavigationInterface(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
        _simStartInputHandler = new SimulationStartInputHandler();
    }

    public void StartSimulation(SimulationStartParametersStandard startParameters)
    {
        if (_pathfindingManager.SimulationStarted)
        {
            UnityEngine.Debug.Log("Request declined. Simulation is already started.");
            return;
        }
        SimulationInputs simulationStartInputs = _simStartInputHandler.HandleInput(startParameters, Allocator.TempJob);
        _pathfindingManager.StartSimulation(simulationStartInputs);
        simulationStartInputs.Dispose();
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
        if (!_pathfindingManager.SimulationStarted) { return; }
        if (agent.AgentDataIndex != -1) { return; }
        _pathfindingManager.RequestAccumulator.RequestAgentAddition(agent);
    }
    public void RequestUnsubscription(FlowFieldAgent agent)
    {
        if (!_pathfindingManager.SimulationStarted) { return; }
        int agentDataIndex = agent.AgentDataIndex;
        if (agentDataIndex == -1) { return; }
        agent.AgentDataIndex = -1;
        _pathfindingManager.RequestAccumulator.RequestAgentRemoval(agentDataIndex);
    }
    public void SetHoldGround(FlowFieldAgent agent)
    {
        if (!_pathfindingManager.SimulationStarted) { return; }
        int agentDataIndex = agent.AgentDataIndex;
        if(agent.AgentDataIndex == -1) { return; }
        _pathfindingManager.RequestAccumulator.RequestHoldGround(agentDataIndex);
    }
    public void SetStopped(FlowFieldAgent agent)
    {
        if (!_pathfindingManager.SimulationStarted) { return; }
        int agentDataIndex = agent.AgentDataIndex;
        if(agent.AgentDataIndex == -1) { return; }
        _pathfindingManager.RequestAccumulator.RequestStop(agentDataIndex);
    }
    public void SetSpeed(FlowFieldAgent agent, float speed)
    {
        if (!_pathfindingManager.SimulationStarted) { return; }
        int agentDataIndex = agent.AgentDataIndex;
        if (agent.AgentDataIndex == -1) { return; }
        _pathfindingManager.RequestAccumulator.RequestSetSpeed(agentDataIndex, speed);
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
public struct SimulationStartParametersStandard
{
    internal FlowFieldSurface[] NavigationSurfaces;
    internal FlowFieldStaticObstacle[] StaticObstacles;
    internal float BaseAgentSpatialGridSize;
    internal float BaseTriangleSpatialGridSize;
    internal float MaxSurfaceHeightDifference;
    internal float TileSize;
    internal float MaxWalkableHeight;
    internal float VerticalVoxelSize;
    internal float MaxAgentRadius;
    internal int LineOfSightRange;
    internal Vector2 FieldStartPositionXZ;
    internal Vector2 FieldEndPositionXZ;

    public SimulationStartParametersStandard(FlowFieldSurface[] navigationSurfaces,
        FlowFieldStaticObstacle[] staticObstacles,
        float baseAgentSpatialGridSize,
        float baseTriangleSpatialGridSize,
        float maxAgentRadius,
        float maxSurfaceHeightDifference,
        float tileSize,
        float verticalVoxelSize,
        int lineOfSightRange,
        float maxWalkableHeight = float.MaxValue)
    {
        NavigationSurfaces = navigationSurfaces;
        StaticObstacles = staticObstacles;
        BaseAgentSpatialGridSize = Mathf.Max(baseAgentSpatialGridSize, 0.1f);
        BaseTriangleSpatialGridSize = Mathf.Max(baseTriangleSpatialGridSize, 0.1f);
        VerticalVoxelSize = Mathf.Max(verticalVoxelSize, 0.01f);
        MaxSurfaceHeightDifference = Mathf.Max(maxSurfaceHeightDifference, VerticalVoxelSize);
        TileSize = Mathf.Max(tileSize, 0.25f);
        MaxWalkableHeight = maxWalkableHeight;
        MaxAgentRadius = Mathf.Max(maxAgentRadius, 0.2f);
        LineOfSightRange = Mathf.Max(lineOfSightRange, 0);
        
        FieldStartPositionXZ = new Vector2(float.MinValue, float.MinValue);
        FieldEndPositionXZ = new Vector2(float.MaxValue, float.MaxValue);
    }
}
public enum Walkability : byte
{
    Unwalkable,
    Walkable
}