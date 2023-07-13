using System.Collections.Generic;
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
    public List<FlowFieldAgent> Agents;

    float _lastAgentUpdateTime = 0;
    PathfindingUpdateRoutine _pathfindingUpdateRoutine;
    AgentUpdater _agentUpdater;
    private void Awake()
    {
        Agents = new List<FlowFieldAgent>();
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
        PathProducer.Update();
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
    public Path SetDestination(NativeArray<Vector3> sources, Vector3 target)
    {
        Vector2 target2 = new Vector2(target.x, target.z);
        return _pathfindingUpdateRoutine.RequestPath(sources, target2, 0);
    }
    public void EditCost(int2 startingPoint, int2 endPoint, byte newCost)
    {
        _pathfindingUpdateRoutine.RequestCostEdit(startingPoint, endPoint, newCost);
    }
    public void Subscribe(FlowFieldAgent agent)
    {
        Agents.Add(agent);
    }
    public void UnSubscribe(FlowFieldAgent agent)
    {
        Agents.Remove(agent);
    }
}
