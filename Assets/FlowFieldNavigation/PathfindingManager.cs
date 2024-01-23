using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

public class PathfindingManager : MonoBehaviour
{
    [SerializeField] internal int LineOfSightRange;
    [SerializeField] float _baseTriangleSpatialGridSize;
    public bool SimulationStarted { get; private set; }
    public FlowFieldNavigationInterface Interface { get; private set; }

    internal FieldDataContainer FieldDataContainer;
    internal PathDataContainer PathDataContainer;
    internal AgentDataContainer AgentDataContainer;
    internal FlockDataContainer FlockDataContainer;
    internal RequestAccumulator RequestAccumulator;

    NavigationUpdater _navigationUpdater;
    void Awake()
    {
        Interface = new FlowFieldNavigationInterface(this);
    }
    void Update()
    {
        if (!SimulationStarted) { return; }
        _navigationUpdater.IntermediateUpdate();
    }
    void FixedUpdate()
    {
        if (!SimulationStarted) { return; }
        _navigationUpdater.RoutineFixedUpdate();
    }
    void LateUpdate()
    {
        if (!SimulationStarted) { return; }
        _navigationUpdater.IntermediateUpdate();
    }
    void SetFlowFieldUtilities(SimulationStartParameters startParameters)
    {
        int sectorColAmount = 10;
        float baseAgentSpatialGridSize = startParameters.BaseAgentSpatialGridSize;
        float tileSize = startParameters.TileSize;
        int rowAmount = startParameters.RowCount;
        int columnAmount = startParameters.ColumCount;
        int sectorMatrixColAmount = columnAmount / sectorColAmount;
        int sectorMatrixRowAmount = rowAmount / sectorColAmount;
        FlowFieldUtilities.DebugMode = false;
        FlowFieldUtilities.SectorMatrixTileAmount = sectorMatrixColAmount * sectorMatrixRowAmount;
        FlowFieldUtilities.SectorMatrixRowAmount = sectorMatrixRowAmount;
        FlowFieldUtilities.SectorMatrixColAmount = sectorMatrixColAmount;
        FlowFieldUtilities.SectorColAmount = sectorColAmount;
        FlowFieldUtilities.SectorRowAmount = sectorColAmount;
        FlowFieldUtilities.SectorTileAmount = sectorColAmount * sectorColAmount;
        FlowFieldUtilities.TileSize = tileSize;
        FlowFieldUtilities.FieldColAmount = columnAmount;
        FlowFieldUtilities.FieldRowAmount = rowAmount;
        FlowFieldUtilities.FieldTileAmount = columnAmount * rowAmount;
        FlowFieldUtilities.BaseAgentSpatialGridSize = baseAgentSpatialGridSize;
        FlowFieldUtilities.BaseTriangleSpatialGridSize = _baseTriangleSpatialGridSize;
        FlowFieldUtilities.MinAgentSize = 0;
        FlowFieldUtilities.MaxAgentSize = (startParameters.MaxCostFieldOffset * tileSize * 2) + tileSize;
        FlowFieldUtilities.LOSRange = LineOfSightRange;
        FlowFieldUtilities.FieldMinXIncluding = startParameters.FieldStartPositionXZ.x;
        FlowFieldUtilities.FieldMinYIncluding = startParameters.FieldStartPositionXZ.y;
        FlowFieldUtilities.FieldMaxXExcluding = startParameters.FieldStartPositionXZ.x + FlowFieldUtilities.FieldColAmount * FlowFieldUtilities.TileSize;
        FlowFieldUtilities.FieldMaxYExcluding = startParameters.FieldStartPositionXZ.y + FlowFieldUtilities.FieldRowAmount * FlowFieldUtilities.TileSize;
        FlowFieldUtilities.MaxCostFieldOffset = startParameters.MaxCostFieldOffset;
        FlowFieldUtilities.FieldGridStartPosition = startParameters.FieldStartPositionXZ;
    }
    internal void StartSimulation(SimulationStartParameters startParameters)
    {
        SimulationStarted = true;
        SetFlowFieldUtilities(startParameters);
        FieldDataContainer = new FieldDataContainer(startParameters.WalkabilityMatrix, startParameters.Meshes, startParameters.Transforms);
        FieldDataContainer.CreateField(startParameters.MaxCostFieldOffset);
        AgentDataContainer = new AgentDataContainer();
        PathDataContainer = new PathDataContainer(this);
        RequestAccumulator = new RequestAccumulator(this);
        _navigationUpdater = new NavigationUpdater(this, RequestAccumulator);
        FlockDataContainer = new FlockDataContainer();
    }
    public void StopSimulation()
    {
        if (!SimulationStarted)
        {
            UnityEngine.Debug.Log("Request Denied. Simulation is already not started");
            return;
        }
        SimulationStarted = false;
        FieldDataContainer.DisposeAll();
        PathDataContainer.DisposeAll();
        AgentDataContainer.DisposeAll();
        FlockDataContainer.DisposeAll();
        RequestAccumulator.DisposeAll();
        _navigationUpdater.DisposeAll();
    }
    internal NativeArray<UnsafeList<HashTile>> GetSpatialHashGridArray()
    {
        return _navigationUpdater.GetRoutineScheduler().GetMovementManager().HashGridArray;
    }
    internal NativeArray<int> GetNormalToHashed()
    {
        return _navigationUpdater.GetRoutineScheduler().GetMovementManager().NormalToHashed;
    }
    internal NativeArray<AgentMovementData> GetAgentMovementData()
    {
        return _navigationUpdater.GetRoutineScheduler().GetMovementManager().AgentMovementDataList;
    }
    internal UnsafeListReadOnly<byte>[] GetAllCostFieldCostsAsUnsafeListReadonly()
    {
        return FieldDataContainer.GetAllCostFieldCostsAsUnsafeListReadonly();
    }
}