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
    public NativeArray<Vector3> TilePositions;
    public List<FlowFieldAgent> Agents;
    public List<Transform> AgentTransforms;

    float _lastAgentUpdateTime = 0;
    AgentUpdateRoutine _agentUpdateRoutine;
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
        TilePositions = new NativeArray<Vector3>(RowAmount * ColumnAmount, Allocator.Persistent);
        _agentUpdateRoutine = new AgentUpdateRoutine();
        CalculateTilePositions();
        CostFieldProducer.ForceCompleteCostFieldProduction();

        SetFlowFieldUtilities();
    }
    private void Update()
    {
        PathProducer.Update();
        float curTime = Time.realtimeSinceStartup;
        float deltaTime = curTime - _lastAgentUpdateTime;
        if (deltaTime >= _agentUpdateFrequency)
        {
            _lastAgentUpdateTime = curTime;
            _agentUpdateRoutine.Update(deltaTime);
        }
    }
    void CalculateTilePositions()
    {
        for (int r = 0; r < RowAmount; r++)
        {
            for (int c = 0; c < ColumnAmount; c++)
            {
                int index = r * ColumnAmount + c;
                TilePositions[index] = new Vector3(TileSize / 2 + c * TileSize, 0f, TileSize / 2 + r * TileSize);
            }
        }
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
        return PathProducer.ProducePath(sources, target, 0);
    }
    public void Subscribe(FlowFieldAgent agent)
    {
        Agents.Add(agent);
        AgentTransforms.Add(agent.transform);
    }
    public void UnSubscribe(FlowFieldAgent agent)
    {
        Agents.Remove(agent);
        AgentTransforms.Remove(agent.transform);
    }
    public void GetIndexAtPos(Vector3 pos, out int local1d, out int sector1d)
    {
        int2 sector2d = new int2(Mathf.FloorToInt(pos.x / (SectorTileAmount * TileSize)), Mathf.FloorToInt(pos.z / (SectorTileAmount * TileSize)));
        int2 general2d = new int2(Mathf.FloorToInt(pos.x / TileSize), Mathf.FloorToInt(pos.z / TileSize));
        int2 sectorStart2d = sector2d * SectorTileAmount;
        int2 local2d = general2d - sectorStart2d;
        local1d = local2d.y * SectorTileAmount + local2d.x;
        sector1d = sector2d.y * SectorMatrixColAmount + sector2d.x;
    }
}
