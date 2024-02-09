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

    public void StartSimulation(SimulationStartParameters startParameters)
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
public class SimulationStartParameters
{
    internal static Vector2 InvalidFieldStartPos { get; } = new Vector2(float.MaxValue, float.MaxValue);
    internal static Vector2 InvalidFieldEndPos { get; } = new Vector2(float.MaxValue, float.MaxValue);

    internal FlowFieldSurface[] NavigationSurfaces;
    internal FlowFieldStaticObstacle[] StaticObstacles;
    internal Walkability[][] WalkabilityData;
    internal float BaseAgentSpatialGridSize;
    internal float MaxSurfaceHeightDifference;
    internal float TileSize;
    internal float MaxWalkableHeight;
    internal float VerticalVoxelSize;
    internal int MaxCostFieldOffset;
    internal int RowCount;
    internal int ColumnCount;
    internal Vector2 FieldStartPositionXZ;
    internal Vector2 FieldEndPositionXZ;
    public SimulationStartParameters(Walkability[][] baseWalkabilityData,
        FlowFieldSurface[] navigationSurfaces,
        FlowFieldStaticObstacle[] staticObstacles,
        float baseAgentSpatialGridSize,
        int maxCostFieldOffset,
        float maxSurfaceHeightDifference,
        float tileSize,
        Vector2 fieldStartPositionXZ,
        float verticalVoxelSize,
        float maxWalkableHeight = float.MaxValue)
    {
        BaseAgentSpatialGridSize = baseAgentSpatialGridSize;
        MaxCostFieldOffset = maxCostFieldOffset;
        NavigationSurfaces = navigationSurfaces;
        StaticObstacles = staticObstacles;
        MaxSurfaceHeightDifference = maxSurfaceHeightDifference;
        TileSize = tileSize;
        RowCount = baseWalkabilityData.Length;
        ColumnCount = baseWalkabilityData[0].Length;
        FieldStartPositionXZ = fieldStartPositionXZ;
        FieldEndPositionXZ = InvalidFieldEndPos;
        MaxWalkableHeight = maxWalkableHeight;
        VerticalVoxelSize = verticalVoxelSize;
        WalkabilityData = baseWalkabilityData;
    }
}
public enum Walkability : byte
{
    Unwalkable,
    Walkable
}