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
        _pathfindingRoutineUpdater = new PathfindingUpdateRoutine(this, PathContainer);
        _agentUpdater = new AgentUpdater(AgentDataContainer);
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
    public void EditCost(int2 startingPoint, int2 endPoint, byte newCost)
    {
        _pathfindingRoutineUpdater.RequestCostEdit(startingPoint, endPoint, newCost);
    }
    public void RequestSubscription(FlowFieldAgent agent)
    {
        _pathfindingRoutineUpdater.RequestAgentAddition(agent);
    }
    public void UnSubscribe(FlowFieldAgent agent)
    {
        AgentDataContainer.UnSubscribe(agent);
    }
    public Path GetPath(int agentIndex)
    {
        int curPathIndex = AgentDataContainer.AgentCurPathIndicies[agentIndex];
        if(curPathIndex == -1) { return null; }
        return PathContainer.ProducedPaths[curPathIndex];
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

    //TRY
    Vector3 hitpos;
    private void OnDrawGizmos()
    {/*
        int sectorTileAmount = FlowFieldUtilities.SectorTileAmount;
        int sectorColAmount = FlowFieldUtilities.SectorColAmount;

        if (Input.GetMouseButton(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                hitpos = hit.point;
            }
        }
        int offset = 1;
        Color[] colors = { Color.black, Color.cyan, Color.magenta, Color.gray, Color.gray, Color.red, Color.black, Color.yellow };
        while(offset < 10)
        {
            Gizmos.color = colors[offset % colors.Length];
            float2 pos = new float2(hitpos.x, hitpos.z);
            int2 start2d = FlowFieldUtilities.PosTo2D(pos, TileSize);

            int2 topLeft = start2d + new int2(-offset, offset);
            int2 topRight = start2d + new int2(offset, offset);
            int2 botLeft = start2d + new int2(-offset, -offset);
            int2 botRight = start2d + new int2(offset, -offset);

            bool topOverflow = topLeft.y >= FlowFieldUtilities.FieldRowAmount;
            bool botOverflow = botLeft.y < 0;
            bool rightOverflow = topRight.x >= FlowFieldUtilities.FieldColAmount;
            bool leftOverflow = topLeft.x < 0;

            if (topOverflow)
            {
                topLeft.y = FlowFieldUtilities.FieldRowAmount - 1;
                topRight.y = FlowFieldUtilities.FieldRowAmount - 1;
            }
            if (botOverflow)
            {
                botLeft.y = 0;
                botRight.y = 0;
            }
            if (rightOverflow)
            {
                botRight.x = FlowFieldUtilities.FieldColAmount - 1;
                topRight.x = FlowFieldUtilities.FieldColAmount - 1;
            }
            if (leftOverflow)
            {
                topLeft.x = 0;
                botLeft.x = 0;
            }

            int topLeftSector = FlowFieldUtilities.GetSector1D(topLeft, sectorColAmount, FlowFieldUtilities.SectorMatrixColAmount);
            int topRightSector = FlowFieldUtilities.GetSector1D(topRight, sectorColAmount, FlowFieldUtilities.SectorMatrixColAmount);
            int botRightSector = FlowFieldUtilities.GetSector1D(botRight, sectorColAmount, FlowFieldUtilities.SectorMatrixColAmount);
            int botLeftSector = FlowFieldUtilities.GetSector1D(botLeft, sectorColAmount, FlowFieldUtilities.SectorMatrixColAmount);
            if (!topOverflow)
            {
                int rowToCheck = topLeft.y % FlowFieldUtilities.SectorRowAmount;
                for (int i = topLeftSector; i <= topRightSector; i++)
                {
                    int colStart = math.select(0, topLeft.x % FlowFieldUtilities.SectorColAmount, i == topLeftSector);
                    int colEnd = math.select(10, topRight.x % FlowFieldUtilities.SectorColAmount, i == topRightSector);
                    CheckSectorRow(i, rowToCheck, colStart, colEnd);
                }
            }
            if (!rightOverflow)
            {
                int colToCheck = topRight.x % FlowFieldUtilities.SectorColAmount;
                for (int i = topRightSector; i >= botRightSector; i -= FlowFieldUtilities.SectorMatrixColAmount)
                {
                    int rowStart = math.select(9, topRight.y % FlowFieldUtilities.SectorRowAmount, i == topRightSector);
                    int rowEnd = math.select(-1, botRight.y % FlowFieldUtilities.SectorRowAmount, i == botRightSector);
                    CheckSectorCol(i, colToCheck, rowStart, rowEnd);
                }
            }
            if (!botOverflow)
            {
                int rowToCheck = botRight.y % FlowFieldUtilities.SectorRowAmount;
                for (int i = botRightSector; i >= botLeftSector; i--)
                {
                    int colStart = math.select(9, botRight.x % FlowFieldUtilities.SectorColAmount, i == botRightSector);
                    int colEnd = math.select(-1, botLeft.x % FlowFieldUtilities.SectorColAmount, i == botLeftSector);
                    CheckSectorRow(i, rowToCheck, colStart, colEnd);
                }
            }
            if (!leftOverflow)
            {
                int colToCheck = topLeft.x % FlowFieldUtilities.SectorColAmount;
                for (int i = botLeftSector; i <= topLeftSector; i += FlowFieldUtilities.SectorMatrixColAmount)
                {
                    int rowStart = math.select(0, botLeft.y % FlowFieldUtilities.SectorRowAmount, i == botLeftSector);
                    int rowEnd = math.select(10, topLeft.y % FlowFieldUtilities.SectorRowAmount, i == topLeftSector);
                    CheckSectorCol(i, colToCheck, rowStart, rowEnd);
                }
            }
            offset++;
        }
        
        void CheckSectorRow(int sectorToCheck, int rowToCheck, int colToStart, int colToEnd)
        {
            int sectorStride = sectorToCheck * sectorTileAmount;
            int startLocal = rowToCheck * sectorColAmount + colToStart;
            int checkRange = colToEnd - colToStart;
            int checkCount = math.abs(checkRange);
            int checkCountNonZero = math.select(checkCount, 1, checkCount == 0);
            int checkUnit = checkRange / checkCountNonZero;

            int startIndex = sectorStride + startLocal;
            for (int i = 0; i < checkCount; i++)
            {
                int indexToCheck = startIndex + i * checkUnit;
                float2 debugPos = FlowFieldUtilities.LocalIndexToPos(indexToCheck - sectorStride, sectorToCheck, SectorMatrixColAmount, SectorColAmount, TileSize, TileSize * sectorColAmount);
                float3 debugPos3 = new float3(debugPos.x, 0, debugPos.y);
                Gizmos.DrawCube(debugPos3, new Vector3(0.3f, 0.3f, 0.3f));
            }
        }
        void CheckSectorCol(int sectorToCheck, int colToCheck, int rowToStart, int rowToEnd)
        {
            int sectorStride = sectorToCheck * sectorTileAmount;
            int startLocal = rowToStart * sectorColAmount + colToCheck;
            int checkRange = rowToEnd - rowToStart;
            int checkCount = math.abs(checkRange);
            int checkCountNonZero = math.select(checkCount, 1, checkCount == 0);
            int checkUnit = checkRange / checkCountNonZero;

            int startIndex = sectorStride + startLocal;
            for (int i = 0; i < checkCount; i++)
            {
                int indexToCheck = startIndex + i * sectorColAmount * checkUnit;
                float2 debugPos = FlowFieldUtilities.LocalIndexToPos(indexToCheck - sectorStride, sectorToCheck, SectorMatrixColAmount, SectorColAmount, TileSize, TileSize * sectorColAmount);
                float3 debugPos3 = new float3(debugPos.x, 0, debugPos.y);
                Gizmos.DrawCube(debugPos3, new Vector3(0.3f, 0.3f, 0.3f));
            }
        }*/
    }
    
}
