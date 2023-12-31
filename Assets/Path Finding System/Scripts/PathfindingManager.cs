using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using System.Diagnostics;
using static UnityEngine.GraphicsBuffer;
using Unity.Burst.CompilerServices;
using System;

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

    float3 p1;
    float3 p2;
    private void OnDrawGizmos()
    {/*
        if (Input.GetMouseButton(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                p1 = hit.point;
            }
        }
        if (Input.GetMouseButton(2))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                p2 = hit.point;
            }
        }
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(p1, 0.2f);
        Gizmos.color = Color.blue;
        Gizmos.DrawCube(p2, new Vector3(0.3f, 0.3f, 0.3f));
        Gizmos.color = Color.black;
        Gizmos.DrawLine(p2, p1);

        Gizmos.color = Color.white;
        float2 point1 = new float2(p1.x, p1.z);
        float2 point2 = new float2(p2.x, p2.z);
        LineCast(point1, point2);*/

        bool LineCast(float2 point1, float2 point2)
        {
            float2 leftPoint = math.select(point2, point1, point1.x < point2.x);
            float2 rightPoint = math.select(point1, point2, point1.x < point2.x);
            float2 dif = rightPoint - leftPoint;
            float slope = dif.y / dif.x;
            float c = leftPoint.y - (slope * leftPoint.x);
            int2 point1Index = (int2)math.floor(point1 / TileSize);
            int2 point2Index = (int2)math.floor(point2 / TileSize);
            if (point1Index.x == point2Index.x || dif.x == 0)
            {
                int startY = (int)math.floor(math.select(point2.y, point1.y, point1.y < point2.y) / TileSize);
                int endY = (int)math.floor(math.select(point2.y, point1.y, point1.y > point2.y) / TileSize);
                for (int y = startY; y <= endY; y++)
                {
                    int2 index = new int2(point1Index.x, y);
                    float2 pos = FlowFieldUtilities.IndexToPos(index, TileSize);
                    float3 pos3 = new float3(pos.x, 0f, pos.y);
                    Gizmos.DrawCube(pos3, new Vector3(0.3f, 0.3f, 0.3f));
                }
                return false;
            }
            if(dif.y == 0)
            {
                int startX = (int)math.floor(math.select(point2.x, point1.x, point1.x < point2.x) / TileSize);
                int endX = (int)math.floor(math.select(point2.x, point1.x, point1.x > point2.x) / TileSize);
                for (int x = startX; x <= endX; x++)
                {
                    int2 index = new int2(x, point1Index.y);
                    float2 pos = FlowFieldUtilities.IndexToPos(index, TileSize);
                    float3 pos3 = new float3(pos.x, 0f, pos.y);
                    Gizmos.DrawCube(pos3, new Vector3(0.3f, 0.3f, 0.3f));
                }
                return false;
            }


            //HANDLE START
            float2 startPoint = leftPoint;
            float nextPointX = math.ceil(startPoint.x / TileSize) * TileSize;
            float2 nextPoint = new float2(nextPointX, c + slope * nextPointX);
            int2 startIndex = (int2)math.floor(startPoint / TileSize);
            int2 nextIndex = (int2)math.floor(nextPoint / TileSize);
            int minY = math.select(nextIndex.y, startIndex.y, startIndex.y < nextIndex.y);
            int maxY = math.select(startIndex.y, nextIndex.y, startIndex.y < nextIndex.y);
            for (int y = minY; y <= maxY; y++)
            {
                int2 index = new int2(startIndex.x, y);
                float2 pos = FlowFieldUtilities.IndexToPos(index, TileSize);
                float3 pos3 = new float3(pos.x, 0f, pos.y);
                Gizmos.DrawCube(pos3, new Vector3(0.3f, 0.3f, 0.3f));
            }

            //HANDLE END
            float2 endPoint = rightPoint;
            float prevPointX = math.floor(endPoint.x / TileSize) * TileSize;
            float2 prevPoint = new float2(prevPointX, c + slope * prevPointX);
            int2 endIndex = (int2)math.floor(endPoint / TileSize);
            int2 prevIndex = (int2)math.floor(prevPoint / TileSize);
            minY = math.select(prevIndex.y, endIndex.y, endIndex.y < prevIndex.y);
            maxY = math.select(endIndex.y, prevIndex.y, endIndex.y < prevIndex.y);
            for (int y = minY; y <= maxY; y++)
            {
                int2 index = new int2(endIndex.x, y);
                float2 pos = FlowFieldUtilities.IndexToPos(index, TileSize);
                float3 pos3 = new float3(pos.x, 0f, pos.y);
                Gizmos.DrawCube(pos3, new Vector3(0.3f, 0.3f, 0.3f));
            }

            //HANDLE MIDDLE
            float curPointY = nextPoint.y;
            float curPointX = nextPoint.x;
            int curIndexX = nextIndex.x;
            int stepCount = (endIndex.x - startIndex.x) - 1;
            for(int i = 0; i < stepCount; i++)
            {
                float newPointX = curPointX + TileSize;
                float newtPointY = slope * newPointX + c;
                int curIndexY = (int)math.floor(curPointY / TileSize);
                int newIndexY = (int)math.floor(newtPointY / TileSize);
                minY = math.select(curIndexY, newIndexY, newIndexY < curIndexY);
                maxY = math.select(newIndexY, curIndexY, newIndexY < curIndexY);
                for(int y = minY; y <= maxY; y++)
                {
                    int2 index = new int2(curIndexX, y);
                    float2 pos = FlowFieldUtilities.IndexToPos(index, TileSize);
                    float3 pos3 = new float3(pos.x, 0f, pos.y);
                    Gizmos.DrawCube(pos3, new Vector3(0.3f, 0.3f, 0.3f));
                }
                curIndexX++;
                curPointY = newtPointY;
                curPointX = newPointX;
            }
            return false;
        }
    }
}