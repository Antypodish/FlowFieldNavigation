using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
public class PathfindingManager : MonoBehaviour
{
    [SerializeField] TerrainGenerator _terrainGenerator;
    [SerializeField] int _maxCostfieldOffset;
    [SerializeField] public float _agentUpdateFrequency;

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

        FieldProducer = new FieldProducer(_terrainGenerator.WalkabilityData, SectorColAmount);
        FieldProducer.CreateField(_maxCostfieldOffset, SectorColAmount, SectorMatrixColAmount, SectorMatrixRowAmount, RowAmount, ColumnAmount, TileSize);

        PathProducer = new PathProducer(this);
        _pathfindingUpdateRoutine = new PathfindingUpdateRoutine(this, PathProducer);
        _agentUpdater = new AgentUpdater(AgentDataContainer, this);

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
        FlowFieldUtilities.SectorColAmount = SectorColAmount;
        FlowFieldUtilities.SectorRowAmount = SectorColAmount;
        FlowFieldUtilities.SectorTileAmount = SectorColAmount * SectorColAmount;
        FlowFieldUtilities.TileSize = TileSize;
        FlowFieldUtilities.FieldColAmount = ColumnAmount;
        FlowFieldUtilities.FieldRowAmount = RowAmount;
        FlowFieldUtilities.FieldTileAmount = ColumnAmount * RowAmount;
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

    private void OnDrawGizmos()
    {
        DebugWallObjects();
        void DebugWallObjects()
        {
            if (FieldProducer == null) { return; }
            Color[] clrs = new Color[] { Color.cyan, Color.black, Color.blue, Color.magenta, Color.green, Color.red, Color.yellow };
            NativeList<WallObject> walls = FieldProducer.GetWallObjectList();
            NativeList<float2> verticies = FieldProducer.GetVertexSequence();
            for (int i = 1; i < walls.Length; i++)
            {
                Gizmos.color = clrs[i % clrs.Length];
                int start = walls[i].vertexStart;
                int len = walls[i].vertexLength;
                for(int j = 0; j < len; j++)
                {
                    int index1 = j + start;
                    int index2 = ((j + 1) % len) + start;
                    Vector3 pos1 = new Vector3(verticies[index1].x, 0.1f, verticies[index1].y);
                    Vector3 pos2 = new Vector3(verticies[index2].x, 0.1f, verticies[index2].y);
                    Gizmos.DrawLine(pos1, pos2);
                }
            }
        }
        void DebugEdgeDirections()
        {
            if(FieldProducer == null) { return; }
            Gizmos.color = Color.black;
            NativeList<Direction> dirs = FieldProducer.GetEdgeDirections();
            NativeList<WallObject> walls = FieldProducer.GetWallObjectList();
            NativeList<float2> verticies = FieldProducer.GetVertexSequence();
            for(int i = 1; i < walls.Length; i++)
            {
                WallObject wall = walls[i];
                for(int j = wall.vertexStart; j < wall.vertexLength + wall.vertexStart - 1; j++)
                {
                    float2 v1 = verticies[j];
                    float2 v2 = verticies[j + 1];
                    Direction dir = dirs[j];
                    float2 avPos = (v1 + v2) / 2f;
                    DrawDir(dir, avPos);
                }
            }

            void DrawDir(Direction dir, float2 avgPos)
            {
                Vector3 start = new Vector3(avgPos.x, 0.1f, avgPos.y);
                switch (dir)
                {
                    case Direction.N:
                        Gizmos.DrawLine(start, start + new Vector3(0, 0, 0.35f));
                        break;
                    case Direction.E:
                        Gizmos.DrawLine(start, start + new Vector3(0.35f, 0, 0));
                        break;
                    case Direction.S:
                        Gizmos.DrawLine(start, start + new Vector3(0, 0, -0.35f));
                        break;
                    case Direction.W:
                        Gizmos.DrawLine(start, start + new Vector3(-0.35f, 0, 0));
                        break;
                }
            }
        }
    }
}
