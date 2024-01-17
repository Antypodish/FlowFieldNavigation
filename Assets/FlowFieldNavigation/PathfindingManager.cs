using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using System.Diagnostics;

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
    public HeightMapProducer HeightMapGenerator;

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
        FlowFieldUtilities.FieldMaxYExcluding = FlowFieldUtilities.FieldRowAmount * FlowFieldUtilities.TileSize;
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
        HeightMapGenerator = new HeightMapProducer();
        HeightMapGenerator.GenerateHeightMap(startParameters.Meshes, startParameters.Transforms);
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
    /*
    float3 hitpos;
    float2 hitpos2;
    float tileSize = 0.1f;
    private void OnDrawGizmos()
    {
        if (HeightMapGenerator == null) { return; }
        Gizmos.color = Color.red;
        Stopwatch sw = new Stopwatch();
        sw.Start();
        if (Input.GetMouseButton(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 1000, 8))
            {
                hitpos = hit.point;
                hitpos2 = new float2(hitpos.x, hitpos.z);
            }
        }
        Gizmos.color = Color.white;
        Gizmos.DrawSphere(hitpos, 0.1f);
        float3 botleft = hitpos + new float3(-tileSize, 0, -tileSize);
        float3 topleft = hitpos + new float3(-tileSize, 0, tileSize);
        float3 topright = hitpos + new float3(tileSize, 0, tileSize);
        float3 botright = hitpos + new float3(tileSize, 0, -tileSize);
        Gizmos.DrawLine(botleft, topleft);
        Gizmos.DrawLine(topleft, topright);
        Gizmos.DrawLine(topright, botright);
        Gizmos.DrawLine(botright, botleft);

        int2 tile2d = FlowFieldUtilities.PosTo2D(hitpos2, TileSize);
        float2 tileMins = new float2(tile2d.x * TileSize, tile2d.y * TileSize);
        float2 tileMaxes = new float2(tileMins.x + TileSize, tileMins.y + TileSize);
        Gizmos.color = Color.black;
        Gizmos.DrawSphere(new Vector3(tileMins.x, 1f, tileMins.y), 0.1f);
        Gizmos.DrawSphere(new Vector3(tileMaxes.x, 1f, tileMaxes.y), 0.1f);
        Gizmos.color = Color.red;
        NativeArray<int> Triangles = HeightMapGenerator.Triangles;
        NativeArray<float3> Verticies = HeightMapGenerator.Verticies;
        int count = 0;
        for (int i = 0; i < Triangles.Length; i += 3)
        {
            int v1Index = Triangles[i];
            int v2Index = Triangles[i + 1];
            int v3Index = Triangles[i + 2];

            float3 v13d = Verticies[v1Index];
            float3 v23d = Verticies[v2Index];
            float3 v33d = Verticies[v3Index];
            float2 v1 = new float2(v13d.x, v13d.z);
            float2 v2 = new float2(v23d.x, v23d.z);
            float2 v3 = new float2(v33d.x, v33d.z);

            //Does triangle have any point inside tile
            bool2 v1AboveMin = v1 >= tileMins;
            bool2 v1BelowMax = v1 <= tileMaxes;
            bool2 v2AboveMin = v2 >= tileMins;
            bool2 v2BelowMax = v2 <= tileMaxes;
            bool2 v3AboveMin = v3 >= tileMins;
            bool2 v3BelowMax = v3 <= tileMaxes;
            bool2 v1result = (v1AboveMin & v1BelowMax);
            bool2 v2result = (v2AboveMin & v2BelowMax);
            bool2 v3result = (v3AboveMin & v3BelowMax);
            bool hasPointWithinTile = (v1result.x && v1result.y) || (v2result.x && v2result.y) || (v3result.x && v3result.y);
            if (hasPointWithinTile)
            {
                float3 average = (v13d + v23d + v33d) / 3;
                Gizmos.DrawSphere(average, 0.1f);
                count++;
                continue;
            }

            //Does triangle intersect with tile

            float2x2 line1 = new float2x2(v1, v2);
            float2x2 line2 = new float2x2(v2, v3);
            float2x2 line3 = new float2x2(v3, v1);
            bool xMinIntersectsLine1 = DoesIntersectAtX(tileMins.x, tileMins.y, tileMaxes.y, line1.c0, line1.c1);
            bool xMaxIntersectsLine1 = DoesIntersectAtX(tileMaxes.x, tileMins.y, tileMaxes.y, line1.c0, line1.c1);
            bool xMinIntersectsLine2 = DoesIntersectAtX(tileMins.x, tileMins.y, tileMaxes.y, line2.c0, line2.c1);
            bool xMaxIntersectsLine2 = DoesIntersectAtX(tileMaxes.x, tileMins.y, tileMaxes.y, line2.c0, line2.c1);
            bool xMinIntersectsLine3 = DoesIntersectAtX(tileMins.x, tileMins.y, tileMaxes.y, line3.c0, line3.c1);
            bool xMaxIntersectsLine3 = DoesIntersectAtX(tileMaxes.x, tileMins.y, tileMaxes.y, line3.c0, line3.c1);

            bool yMinIntersectsLine1 = DoesIntersectAtY(tileMins.y, tileMins.x, tileMaxes.x, line1.c0, line1.c1);
            bool yMaxIntersectsLine1 = DoesIntersectAtY(tileMaxes.y, tileMins.x, tileMaxes.x, line1.c0, line1.c1);
            bool yMinIntersectsLine2 = DoesIntersectAtY(tileMins.y, tileMins.x, tileMaxes.x, line2.c0, line2.c1);
            bool yMaxIntersectsLine2 = DoesIntersectAtY(tileMaxes.y, tileMins.x, tileMaxes.x, line2.c0, line2.c1);
            bool yMinIntersectsLine3 = DoesIntersectAtY(tileMins.y, tileMins.x, tileMaxes.x, line3.c0, line3.c1);
            bool yMaxIntersectsLine3 = DoesIntersectAtY(tileMaxes.y, tileMins.x, tileMaxes.x, line3.c0, line3.c1);

            bool intersectsX = xMinIntersectsLine1 || xMaxIntersectsLine1 || xMinIntersectsLine2 || xMaxIntersectsLine2 || xMinIntersectsLine3 || xMaxIntersectsLine3;
            bool intersectsY = yMinIntersectsLine1 || yMaxIntersectsLine1 || yMinIntersectsLine2 || yMaxIntersectsLine2 || yMinIntersectsLine3 || yMaxIntersectsLine3;
            if (intersectsX)
            {
                float3 average = (v13d + v23d + v33d) / 3;
                Gizmos.DrawSphere(average, 0.1f);
                count++;
                continue;
            }
            if (intersectsY)
            {
                float3 average = (v13d + v23d + v33d) / 3;
                Gizmos.DrawSphere(average, 0.1f);
                count++;
                continue;
            }
            if(IsPointInsideTriangle(tileMins, v1, v2, v3))
            {
                float3 average = (v13d + v23d + v33d) / 3;
                Gizmos.DrawSphere(average, 0.1f);
                count++;
                continue;
            }
        }
        sw.Stop();
        UnityEngine.Debug.Log(sw.Elapsed.TotalMilliseconds);
        bool DoesIntersectAtX(float xToCheck, float yMin, float yMax, float2 v1, float2 v2)
        {
            float2 vLeft = math.select(v2, v1, v1.x < v2.x);
            float2 vRight = math.select(v1, v2, v1.x < v2.x);
            if (xToCheck <= vLeft.x || xToCheck >= vRight.x) { return false; }

            float t = (xToCheck - vLeft.x) / (vRight.x - vLeft.x);
            float y = vLeft.y + (vRight.y - vLeft.y) * t;
            return y > yMin && y < yMax;
        }
        bool DoesIntersectAtY(float yToCheck, float xmin, float xmax, float2 v1, float2 v2)
        {
            float2 vDown = math.select(v2, v1, v1.y < v2.y);
            float2 vUp = math.select(v1, v2, v1.y < v2.y);
            if (yToCheck <= vDown.y || yToCheck >= vUp.y) { return false; }

            float t = (yToCheck - vDown.y) / (vUp.y - vDown.y);
            float x = vDown.x + (vUp.x - vDown.x) * t;
            return x > xmin && x < xmax;
        }
        bool IsPointInsideTriangle(float2 point, float2 v1, float2 v2, float2 v3)
        {
            float trigMinX = math.min(math.min(v1.x, v2.x), v3.x);
            float trigMaxX = math.max(math.max(v1.x, v2.x), v3.x);
            if(point.x < trigMinX || point.x > trigMaxX) { return false; }

            float2x2 line1 = new float2x2()
            {
                c0 = math.select(v2, v1, v1.x < v2.x),
                c1 = math.select(v1, v2, v1.x < v2.x),
            };
            bool line1ContainsX = point.x >= line1.c0.x && point.x <= line1.c1.x;
            float2x2 line2 = new float2x2()
            {
                c0 = math.select(v3, v2, v2.x < v3.x),
                c1 = math.select(v2, v3, v2.x < v3.x),
            };
            bool line2ContainsX = point.x >= line2.c0.x && point.x <= line2.c1.x;
            float2x2 line3 = new float2x2()
            {
                c0 = math.select(v1, v3, v3.x < v1.x),
                c1 = math.select(v3, v1, v3.x < v1.x),
            };
            bool line3ContainsX = point.x >= line3.c0.x && point.x <= line3.c1.x;

            float tLine1 = (point.x - line1.c0.x) / (line1.c1.x - line1.c0.x);
            float tLine2 = (point.x - line2.c0.x) / (line2.c1.x - line2.c0.x);
            float tLine3 = (point.x - line3.c0.x) / (line3.c1.x - line3.c0.x);
            float yLine1 = line1.c0.y + (line1.c1.y - line1.c0.y) * tLine1;
            float yLine2 = line2.c0.y + (line2.c1.y - line2.c0.y) * tLine2;
            float yLine3 = line3.c0.y + (line3.c1.y - line3.c0.y) * tLine3;

            float yMin = float.MaxValue;
            float yMax = float.MinValue;
            yMin = math.select(yMin, yLine1, yLine1 < yMin && line1ContainsX);
            yMin = math.select(yMin, yLine2, yLine2 < yMin && line2ContainsX);
            yMin = math.select(yMin, yLine3, yLine3 < yMin && line3ContainsX);
            yMax = math.select(yMax, yLine1, yLine1 > yMax && line1ContainsX);
            yMax = math.select(yMax, yLine2, yLine2 > yMax && line2ContainsX);
            yMax = math.select(yMax, yLine3, yLine3 > yMax && line3ContainsX);
            return point.y <= yMax && point.y >= yMin;
        }
    }*/
}
public struct SimulationStartParameters
{
    public float TileSize;
    public int RowCount;
    public int ColumCount;
    public WalkabilityCell[][] WalkabilityMatrix;
    public int MaxCostFieldOffset;
    public float BaseSpatialGridSize;
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