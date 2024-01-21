using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

public class PathfindingManager : MonoBehaviour
{
    [SerializeField] public int LineOfSightRange;
    [SerializeField] float _baseTriangleSpatialGridSize;
    public bool SimulationStarted { get; private set; }

    public FieldManager FieldManager;
    public PathContainer PathContainer;
    public AgentDataContainer AgentDataContainer;
    public FlockDataContainer FlockContainer;

    NavigationUpdater _navigationUpdater;
    RequestAccumulator _requestAccumulator;

    private void Update()
    {
        if (!SimulationStarted) { return; }
        _navigationUpdater.IntermediateUpdate();
    }
    private void FixedUpdate()
    {
        if (!SimulationStarted) { return; }
        _navigationUpdater.RoutineFixedUpdate();
    }
    private void LateUpdate()
    {
        if (!SimulationStarted) { return; }
        _navigationUpdater.IntermediateUpdate();
    }
    void SetFlowFieldUtilities(SimulationStartParameters startParameters)
    {
        int sectorColAmount = 10;
        float baseAgentSpatialGridSize = startParameters.BaseAgentSpatialGridSize;
        float tileSize = startParameters.TileSize;
        int rowAmount = startParameters.RowCount;
        int columnAmount = startParameters.ColumCount;
        int sectorMatrixColAmount = columnAmount / sectorColAmount;
        int sectorMatrixRowAmount = rowAmount / sectorColAmount;
        FlowFieldUtilities.DebugMode = false;
        FlowFieldUtilities.SectorMatrixTileAmount = sectorMatrixColAmount * sectorMatrixRowAmount;
        FlowFieldUtilities.SectorMatrixRowAmount = sectorMatrixRowAmount;
        FlowFieldUtilities.SectorMatrixColAmount = sectorMatrixColAmount;
        FlowFieldUtilities.SectorColAmount = sectorColAmount;
        FlowFieldUtilities.SectorRowAmount = sectorColAmount;
        FlowFieldUtilities.SectorTileAmount = sectorColAmount * sectorColAmount;
        FlowFieldUtilities.TileSize = tileSize;
        FlowFieldUtilities.FieldColAmount = columnAmount;
        FlowFieldUtilities.FieldRowAmount = rowAmount;
        FlowFieldUtilities.FieldTileAmount = columnAmount * rowAmount;
        FlowFieldUtilities.BaseAgentSpatialGridSize = baseAgentSpatialGridSize;
        FlowFieldUtilities.BaseTriangleSpatialGridSize = _baseTriangleSpatialGridSize;
        FlowFieldUtilities.MinAgentSize = 0;
        FlowFieldUtilities.MaxAgentSize = (startParameters.MaxCostFieldOffset * tileSize * 2) + tileSize;
        FlowFieldUtilities.LOSRange = LineOfSightRange;
        FlowFieldUtilities.FieldMinXIncluding = 0f;
        FlowFieldUtilities.FieldMinYIncluding = 0f;
        FlowFieldUtilities.FieldMaxXExcluding = FlowFieldUtilities.FieldColAmount * FlowFieldUtilities.TileSize;
        FlowFieldUtilities.FieldMaxYExcluding = FlowFieldUtilities.FieldRowAmount * FlowFieldUtilities.TileSize;
        FlowFieldUtilities.MaxCostFieldOffset = startParameters.MaxCostFieldOffset;
    }
    public void StartSimulation(SimulationStartParameters startParameters)
    {
        if (SimulationStarted)
        {
            UnityEngine.Debug.Log("Request declined. Simulation is already started.");
            return;
        }
        SimulationStarted = true;
        SetFlowFieldUtilities(startParameters);
        FieldManager = new FieldManager(startParameters.WalkabilityMatrix, startParameters.Meshes, startParameters.Transforms);
        FieldManager.CreateField(startParameters.MaxCostFieldOffset);
        AgentDataContainer = new AgentDataContainer();
        PathContainer = new PathContainer(this);
        _requestAccumulator = new RequestAccumulator(this);
        _navigationUpdater = new NavigationUpdater(this, _requestAccumulator);
        FlockContainer = new FlockDataContainer();
    }
    public void SetDestination(List<FlowFieldAgent> agents, Vector3 target)
    {
        if (agents.Count == 0) { UnityEngine.Debug.Log("Agent list passed is empty"); return; }
        _requestAccumulator.RequestPath(agents, target);
    }
    public void SetDestination(List<FlowFieldAgent> agents, FlowFieldAgent targetAgent)
    {
        if (agents.Count == 0) { UnityEngine.Debug.Log("Agent list passed is empty"); return; }
        _requestAccumulator.RequestPath(agents, targetAgent);
    }
    public NativeArray<UnsafeList<HashTile>> GetSpatialHashGridArray()
    {
        return _navigationUpdater.GetRoutineScheduler().GetRoutineDataProducer().HashGridArray;
    }
    public NativeArray<int> GetNormalToHashed()
    {
        return _navigationUpdater.GetRoutineScheduler().GetRoutineDataProducer().NormalToHashed;
    }
    public NativeArray<AgentMovementData> GetAgentMovementData()
    {
        return _navigationUpdater.GetRoutineScheduler().GetRoutineDataProducer().AgentMovementDataList;
    }
    public void SetObstacle(NativeArray<ObstacleRequest> obstacleRequests, NativeList<int> outputListToAddObstacleIndicies)
    {
        _requestAccumulator.HandleObstacleRequest(obstacleRequests, outputListToAddObstacleIndicies);
    }
    public void RemoveObstacle(NativeArray<int>.ReadOnly obstaclesToRemove)
    {
        _requestAccumulator.HandleObstacleRemovalRequest(obstaclesToRemove);
    }
    public void RequestSubscription(FlowFieldAgent agent)
    {
        _requestAccumulator.RequestAgentAddition(agent);
    }
    public int GetPathIndex(int agentIndex)
    {
        return AgentDataContainer.AgentCurPathIndicies[agentIndex];
    }
    public List<FlowFieldAgent> GetAllAgents()
    {
        return AgentDataContainer.Agents;
    }
    public int GetAgentCount()
    {
        return AgentDataContainer.Agents.Count;
    }
    public UnsafeListReadOnly<byte>[] GetAllCostFieldCostsAsUnsafeListReadonly()
    {
        return FieldManager.GetAllCostFieldCostsAsUnsafeListReadonly();
    }
}
public struct SimulationStartParameters
{
    public float TileSize;
    public int RowCount;
    public int ColumCount;
    public WalkabilityCell[][] WalkabilityMatrix;
    public int MaxCostFieldOffset;
    public float BaseAgentSpatialGridSize;
    public Mesh[] Meshes;
    public Transform[] Transforms;
}
public struct WalkabilityCell
{
    public Vector3 CellPosition;
    public Walkability Walkability;
}
public enum Walkability : byte
{
    Unwalkable,
    Walkable
}