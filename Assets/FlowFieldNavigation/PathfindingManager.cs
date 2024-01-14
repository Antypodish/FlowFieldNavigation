using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

public class PathfindingManager : MonoBehaviour
{
    [SerializeField] public int LineOfSightRange;

    public bool SimulationStarted { get; private set; }

    [HideInInspector] public float TileSize;
    [HideInInspector] public int RowAmount;
    [HideInInspector] public int ColumnAmount;
    [HideInInspector] public byte SectorColAmount = 10;
    [HideInInspector] public int SectorMatrixColAmount;
    [HideInInspector] public int SectorMatrixRowAmount;
    [HideInInspector] public float BaseSpatialGridSize;

    public FieldProducer FieldProducer;
    public PathContainer PathContainer;
    public AgentDataContainer AgentDataContainer;
    public ObstacleContainer ObstacleContainer;
    public FlockDataContainer FlockContainer;

    int _maxCostfieldOffset;
    PathfindingUpdateRoutine _pathfindingRoutineUpdater;
    AgentUpdater _agentUpdater;

    private void Update()
    {
        if (!SimulationStarted) { return; }
        _pathfindingRoutineUpdater.IntermediateLateUpdate();
    }
    private void FixedUpdate()
    {
        if (!SimulationStarted) { return; }
        _agentUpdater.OnUpdate();
        _pathfindingRoutineUpdater.RoutineUpdate();
    }
    private void LateUpdate()
    {
        if (!SimulationStarted) { return; }
        _pathfindingRoutineUpdater.IntermediateLateUpdate();
    }
    void SetFlowFieldUtilities()
    {
        FlowFieldUtilities.DebugMode = false;
        FlowFieldUtilities.SectorMatrixTileAmount = SectorMatrixColAmount * SectorMatrixRowAmount;
        FlowFieldUtilities.SectorMatrixRowAmount = SectorMatrixRowAmount;
        FlowFieldUtilities.SectorMatrixColAmount = SectorMatrixColAmount;
        FlowFieldUtilities.SectorColAmount = SectorColAmount;
        FlowFieldUtilities.SectorRowAmount = SectorColAmount;
        FlowFieldUtilities.SectorTileAmount = SectorColAmount * SectorColAmount;
        FlowFieldUtilities.TileSize = TileSize;
        FlowFieldUtilities.FieldColAmount = ColumnAmount;
        FlowFieldUtilities.FieldRowAmount = RowAmount;
        FlowFieldUtilities.FieldTileAmount = ColumnAmount * RowAmount;
        FlowFieldUtilities.BaseSpatialGridSize = BaseSpatialGridSize;
        FlowFieldUtilities.MinAgentSize = 0;
        FlowFieldUtilities.MaxAgentSize = (_maxCostfieldOffset * TileSize * 2) + TileSize;
        FlowFieldUtilities.LOSRange = LineOfSightRange;
        FlowFieldUtilities.FieldMinXIncluding = 0f;
        FlowFieldUtilities.FieldMinYIncluding = 0f;
        FlowFieldUtilities.FieldMaxXExcluding = FlowFieldUtilities.FieldColAmount * FlowFieldUtilities.TileSize;
        FlowFieldUtilities.FieldMaxYExcluding = FlowFieldUtilities.FieldColAmount * FlowFieldUtilities.TileSize;
        FlowFieldUtilities.MaxCostFieldOffset = _maxCostfieldOffset;
    }
    public void StartSimulation(SimulationStartParameters startParameters)
    {
        if (SimulationStarted)
        {
            UnityEngine.Debug.Log("Request declined. Simulation is already started.");
            return;
        }

        SimulationStarted = true;
        //!!!ORDER IS IMPORTANT!!!
        BaseSpatialGridSize = startParameters.BaseSpatialGridSize;
        TileSize = startParameters.TileSize;
        RowAmount = startParameters.RowCount;
        ColumnAmount = startParameters.ColumCount;
        SectorMatrixColAmount = ColumnAmount / SectorColAmount;
        SectorMatrixRowAmount = RowAmount / SectorColAmount;
        SetFlowFieldUtilities();
        FieldProducer = new FieldProducer(startParameters.WalkabilityMatrix, SectorColAmount);
        FieldProducer.CreateField(startParameters.MaxCostFieldOffset, SectorColAmount, SectorMatrixColAmount, SectorMatrixRowAmount, RowAmount, ColumnAmount, TileSize);
        AgentDataContainer = new AgentDataContainer();
        PathContainer = new PathContainer(this);
        _pathfindingRoutineUpdater = new PathfindingUpdateRoutine(this);
        _agentUpdater = new AgentUpdater(AgentDataContainer);
        ObstacleContainer = new ObstacleContainer();
        FlockContainer = new FlockDataContainer();
    }
    public void SetDestination(List<FlowFieldAgent> agents, Vector3 target)
    {
        if (agents.Count == 0) { UnityEngine.Debug.Log("Agent list passed is empty"); return; }
        _pathfindingRoutineUpdater.RequestPath(agents, target);
    }
    public void SetDestination(List<FlowFieldAgent> agents, FlowFieldAgent targetAgent)
    {
        if (agents.Count == 0) { UnityEngine.Debug.Log("Agent list passed is empty"); return; }
        _pathfindingRoutineUpdater.RequestPath(agents, targetAgent);
    }
    public NativeArray<UnsafeList<HashTile>> GetSpatialHashGridArray()
    {
        return _pathfindingRoutineUpdater.GetRoutineScheduler().GetRoutineDataProducer().HashGridArray;
    }
    public NativeArray<int> GetNormalToHashed()
    {
        return _pathfindingRoutineUpdater.GetRoutineScheduler().GetRoutineDataProducer().NormalToHashed;
    }
    public NativeArray<AgentMovementData> GetAgentMovementData()
    {
        return _pathfindingRoutineUpdater.GetRoutineScheduler().GetRoutineDataProducer().AgentMovementDataList;
    }
    public void SetObstacle(NativeArray<ObstacleRequest> obstacleRequests, NativeList<int> outputListToAddObstacleIndicies)
    {
        _pathfindingRoutineUpdater.HandleObstacleRequest(obstacleRequests, outputListToAddObstacleIndicies);
    }
    public void RemoveObstacle(NativeArray<int>.ReadOnly obstaclesToRemove)
    {
        _pathfindingRoutineUpdater.HandleObstacleRemovalRequest(obstaclesToRemove);
    }
    public void RequestSubscription(FlowFieldAgent agent)
    {
        _pathfindingRoutineUpdater.RequestAgentAddition(agent);
    }
    public void UnSubscribe(FlowFieldAgent agent)
    {
        AgentDataContainer.UnSubscribe(agent);
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
        return FieldProducer.GetAllCostFieldCostsAsUnsafeListReadonly();
    }
}
public struct SimulationStartParameters
{
    public float TileSize;
    public int RowCount;
    public int ColumCount;
    public WalkabilityCell[][] WalkabilityMatrix;
    public int MaxCostFieldOffset;
    public float BaseSpatialGridSize;
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