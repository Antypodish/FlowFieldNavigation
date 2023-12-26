using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using System.Diagnostics;
using static UnityEngine.GraphicsBuffer;
using Unity.Burst.CompilerServices;

public class PathfindingManager : MonoBehaviour
{
    [SerializeField] TerrainGenerator _terrainGenerator;
    [SerializeField] int _maxCostfieldOffset;
    [SerializeField] public float AgentUpdateFrequency;
    [SerializeField] public float BaseSpatialGridSize;
    [SerializeField] public int LineOfSightRange;

    [HideInInspector] public float TileSize;
    [HideInInspector] public int RowAmount;
    [HideInInspector] public int ColumnAmount;
    [HideInInspector] public byte SectorColAmount = 10;
    [HideInInspector] public int SectorMatrixColAmount;
    [HideInInspector] public int SectorMatrixRowAmount;

    public FieldProducer FieldProducer;
    public PathContainer PathContainer;
    public AgentDataContainer AgentDataContainer;
    public ObstacleContainer ObstacleContainer;
    float _lastAgentUpdateTime = 0;
    PathfindingUpdateRoutine _pathfindingRoutineUpdater;
    AgentUpdater _agentUpdater;

    private void Awake()
    {
        //!!!ORDER IS IMPORTANT!!!
        TileSize = _terrainGenerator.TileSize;
        RowAmount = _terrainGenerator.RowAmount;
        ColumnAmount = _terrainGenerator.ColumnAmount;
        SectorMatrixColAmount = ColumnAmount / SectorColAmount;
        SectorMatrixRowAmount = RowAmount / SectorColAmount;
        SetFlowFieldUtilities();


        FieldProducer = new FieldProducer(_terrainGenerator.WalkabilityData, SectorColAmount);
        FieldProducer.CreateField(_maxCostfieldOffset, SectorColAmount, SectorMatrixColAmount, SectorMatrixRowAmount, RowAmount, ColumnAmount, TileSize);

        AgentDataContainer = new AgentDataContainer(this);
        PathContainer = new PathContainer(this);
        _pathfindingRoutineUpdater = new PathfindingUpdateRoutine(this);
        _agentUpdater = new AgentUpdater(AgentDataContainer);
        ObstacleContainer = new ObstacleContainer();
    }
    private void Update()
    {
        _agentUpdater.OnUpdate();
        _pathfindingRoutineUpdater.IntermediateLateUpdate();

        float curTime = Time.realtimeSinceStartup;
        float timePassed = curTime - _lastAgentUpdateTime;
        if (timePassed >= AgentUpdateFrequency)
        {
            _lastAgentUpdateTime = curTime;
            _pathfindingRoutineUpdater.RoutineUpdate(timePassed);
        }
    }
    private void FixedUpdate()
    {
        //_pathfindingRoutineUpdater.RoutineUpdate(Time.fixedDeltaTime);

    }
    private void LateUpdate()
    {
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