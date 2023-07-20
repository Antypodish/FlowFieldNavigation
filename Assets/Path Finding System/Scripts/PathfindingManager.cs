using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
public class PathfindingManager : MonoBehaviour
{
    [SerializeField] TerrainGenerator _terrainGenerator;
    [SerializeField] int _maxCostfieldOffset;
    [SerializeField] float _agentUpdateFrequency;

    [HideInInspector] public float TileSize;
    [HideInInspector] public int RowAmount;
    [HideInInspector] public int ColumnAmount;
    [HideInInspector] public byte SectorTileAmount = 10;
    [HideInInspector] public int SectorMatrixColAmount;
    [HideInInspector] public int SectorMatrixRowAmount;

    public CostFieldProducer CostFieldProducer;
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
        SectorMatrixColAmount = ColumnAmount / SectorTileAmount;
        SectorMatrixRowAmount = RowAmount / SectorTileAmount;
        CostFieldProducer = new CostFieldProducer(_terrainGenerator.WalkabilityData, SectorTileAmount, ColumnAmount, RowAmount, SectorMatrixColAmount, SectorMatrixRowAmount);
        CostFieldProducer.StartCostFieldProduction(0, _maxCostfieldOffset, SectorTileAmount, SectorMatrixColAmount, SectorMatrixRowAmount);
        PathProducer = new PathProducer(this);
        _pathfindingUpdateRoutine = new PathfindingUpdateRoutine(this, PathProducer);
        _agentUpdater = new AgentUpdater(AgentDataContainer, this);
        CostFieldProducer.ForceCompleteCostFieldProduction();

        SetFlowFieldUtilities();
    }
    private void Update()
    {
        _agentUpdater.OnUpdate();
        float curTime = Time.realtimeSinceStartup;
        float deltaTime = curTime - _lastAgentUpdateTime;
        if (deltaTime >= _agentUpdateFrequency)
        {
            _lastAgentUpdateTime = curTime;
            _pathfindingUpdateRoutine.RoutineUpdate(deltaTime);
        }
    }
    private void LateUpdate()
    {
        _pathfindingUpdateRoutine.IntermediateLateUpdate();
    }
    void SetFlowFieldUtilities()
    {
        FlowFieldUtilities.DebugMode = false;
        FlowFieldUtilities.SectorMatrixTileAmount = SectorMatrixColAmount * SectorMatrixRowAmount;
        FlowFieldUtilities.SectorMatrixRowAmount = SectorMatrixRowAmount;
        FlowFieldUtilities.SectorMatrixColAmount = SectorMatrixColAmount;
        FlowFieldUtilities.SectorColAmount = SectorTileAmount;
        FlowFieldUtilities.SectorRowAmount = SectorTileAmount;
        FlowFieldUtilities.SectorTileAmount = SectorTileAmount * SectorTileAmount;
        FlowFieldUtilities.TileSize = TileSize;
        FlowFieldUtilities.FieldColAmount = ColumnAmount;
        FlowFieldUtilities.FieldRowAmount = RowAmount;
        FlowFieldUtilities.FieldTileAmount = ColumnAmount * RowAmount;
    }
    public void SetDestination(List<FlowFieldAgent> agents, Vector3 target)
    {
        if(agents.Count == 0) { UnityEngine.Debug.Log("Agent list passed is empty"); return; }
        NativeArray<float2> sources = AgentDataContainer.GetPositionsOf(agents);
        Vector2 target2 = new Vector2(target.x, target.z);

        //DETERMINE MIN OFFSET
        float maxRadius = 0;
        for(int i = 0; i < agents.Count; i++)
        {
            float radius = agents[i].GetRadius();
            maxRadius = radius > maxRadius ? radius : maxRadius;
        }
        int offset = Mathf.FloorToInt(maxRadius);

        
        //CREATE PATH
        Path newPath = _pathfindingUpdateRoutine.RequestPath(sources, target2, offset);
        
        if (newPath == null) { return; }
        for(int i = 0; i < agents.Count; i++)
        {
            agents[i].SetPath(newPath);
        }
    }
    public void EditCost(int2 startingPoint, int2 endPoint, byte newCost)
    {
        _pathfindingUpdateRoutine.RequestCostEdit(startingPoint, endPoint, newCost);
    }
    public void Subscribe(FlowFieldAgent agent)
    {
        AgentDataContainer.Subscribe(agent);
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
