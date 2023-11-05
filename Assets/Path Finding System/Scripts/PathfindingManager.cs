using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
public class PathfindingManager : MonoBehaviour
{
    [SerializeField] TerrainGenerator _terrainGenerator;
    [SerializeField] int _maxCostfieldOffset;
    [SerializeField] public float AgentUpdateFrequency;
    [SerializeField] public float BaseSpatialGridSize;

    [HideInInspector] public float TileSize;
    [HideInInspector] public int RowAmount;
    [HideInInspector] public int ColumnAmount;
    [HideInInspector] public byte SectorColAmount = 10;
    [HideInInspector] public int SectorMatrixColAmount;
    [HideInInspector] public int SectorMatrixRowAmount;

    public FieldProducer FieldProducer;
    public PathProducer PathProducer;
    public AgentDataContainer AgentDataContainer;

    float _lastAgentUpdateTime = 0;
    PathfindingUpdateRoutine _pathfindingUpdateRoutine;
    AgentUpdater _agentUpdater;
    private void Awake()
    {
        AgentDataContainer = new AgentDataContainer(this);
    }
    private void Start()
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

        PathProducer = new PathProducer(this);
        _pathfindingUpdateRoutine = new PathfindingUpdateRoutine(this, PathProducer);
        _agentUpdater = new AgentUpdater(AgentDataContainer);

        
    }
    private void Update()
    {
        _agentUpdater.OnUpdate();

        float curTime = Time.realtimeSinceStartup;
        float deltaTime = curTime - _lastAgentUpdateTime;
        if (deltaTime >= AgentUpdateFrequency)
        {
            _lastAgentUpdateTime = curTime;
            //_pathfindingUpdateRoutine.RoutineUpdate(deltaTime);
        }
        _pathfindingUpdateRoutine.IntermediateLateUpdate();
    }
    private void FixedUpdate()
    {
        _pathfindingUpdateRoutine.RoutineUpdate(Time.fixedDeltaTime);
    }
    private void LateUpdate()
    {
        //_pathfindingUpdateRoutine.IntermediateLateUpdate();
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
    }
    public void SetDestination(List<FlowFieldAgent> agents, Vector3 target)
    {
        
        NativeArray<float2> sources = AgentDataContainer.GetPositionsOf(agents);
        Vector2 target2 = new Vector2(target.x, target.z);

        //DETERMINE MIN OFFSET
        float maxRadius = 0;
        for(int i = 0; i < agents.Count; i++)
        {
            float radius = agents[i].GetRadius();
            maxRadius = radius > maxRadius ? radius : maxRadius;
        }
        int offset = Mathf.FloorToInt(maxRadius + 0.5f);
        
        if (agents.Count == 0) { UnityEngine.Debug.Log("Agent list passed is empty"); return; }
        //CREATE PATH
        Path newPath = _pathfindingUpdateRoutine.RequestPath(sources, target2, offset);
        if (newPath == null) { return; }
        for(int i = 0; i < agents.Count; i++)
        {
            agents[i].SetPath(newPath);
        }
        
    }
    public NativeArray<UnsafeList<HashTile>> GetSpatialHashGridArray()
    {
        return _pathfindingUpdateRoutine.GetRoutineScheduler().GetRoutineDataProducer().HashGridArray;
    }
    public NativeArray<int> GetNormalToHashed()
    {
        return _pathfindingUpdateRoutine.GetRoutineScheduler().GetRoutineDataProducer().NormalToHashed;
    }
    public NativeArray<AgentMovementData> GetAgentMovementData()
    {
        return _pathfindingUpdateRoutine.GetRoutineScheduler().GetRoutineDataProducer().AgentMovementDataList;
    }
    public void EditCost(int2 startingPoint, int2 endPoint, byte newCost)
    {
        _pathfindingUpdateRoutine.RequestCostEdit(startingPoint, endPoint, newCost);
    }
    public void RequestSubscription(FlowFieldAgent agent)
    {
        _pathfindingUpdateRoutine.RequestAgentAddition(agent);
    }
    public void UnSubscribe(FlowFieldAgent agent)
    {
        AgentDataContainer.UnSubscribe(agent);
    }
    public void SetPath(int agentIndex, Path newPath)
    {
        AgentDataContainer.SetPath(agentIndex, newPath);
    }
    public Path GetPath(int agentIndex)
    {
        return AgentDataContainer.GetPath(agentIndex);
    }
    public List<FlowFieldAgent> GetAllAgents()
    {
        return AgentDataContainer.Agents;
    }
    public int GetAgentCount()
    {
        return AgentDataContainer.Agents.Count;
    }
}
