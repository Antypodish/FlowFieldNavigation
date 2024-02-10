using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using Unity.Mathematics;
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
    internal PathConstructionPipeline PathConstructionPipeline;
    internal MovementManager MovementManager;
    internal AgentRemovingSystem AgentRemovingSystem;

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

    //Handle inappropriate col/row counts
    void SetFlowFieldUtilities(SimulationInputs startInputs)
    {
        const int sectorColAmount = 10;
        const int minRowCount = sectorColAmount * 2; //Does not work below. Can be improved.
        const int minColCount = sectorColAmount * 2; //Does not work below. Can be improved.
        int columnAmount = startInputs.ColumnCount;
        int rowAmount = startInputs.RowCount;
        columnAmount = columnAmount + (sectorColAmount - (columnAmount % sectorColAmount));
        rowAmount = rowAmount + (sectorColAmount - (rowAmount % sectorColAmount));
        rowAmount = math.select(rowAmount, minRowCount, rowAmount < minRowCount);
        columnAmount = math.select(columnAmount, minColCount, columnAmount < minColCount);

        float baseAgentSpatialGridSize = startInputs.BaseAgentSpatialGridSize;
        float tileSize = startInputs.TileSize;
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
        FlowFieldUtilities.MaxAgentSize = startInputs.MaxAgentRadius;
        FlowFieldUtilities.LOSRange = LineOfSightRange;
        FlowFieldUtilities.FieldMinXIncluding = startInputs.FieldStartPositionXZ.x + 0.01f;
        FlowFieldUtilities.FieldMinYIncluding = startInputs.FieldStartPositionXZ.y + 0.01f;
        FlowFieldUtilities.FieldMaxXExcluding = startInputs.FieldStartPositionXZ.x + FlowFieldUtilities.FieldColAmount * FlowFieldUtilities.TileSize - 0.01f;
        FlowFieldUtilities.FieldMaxYExcluding = startInputs.FieldStartPositionXZ.y + FlowFieldUtilities.FieldRowAmount * FlowFieldUtilities.TileSize - 0.01f;
        FlowFieldUtilities.MaxCostFieldOffset = FlowFieldUtilities.RadiusToOffset(startInputs.MaxAgentRadius, tileSize);
        FlowFieldUtilities.FieldGridStartPosition = startInputs.FieldStartPositionXZ;
    }
    internal void StartSimulation(SimulationInputs startInputs)
    {
        SimulationStarted = true;
        SetFlowFieldUtilities(startInputs);
        FieldDataContainer = new FieldDataContainer(startInputs.NavigationSurfaceVerticies, startInputs.NavigationSurfaceTriangles);
        FieldDataContainer.CreateField(startInputs.StaticObstacles, 
            startInputs.TileSize,
            startInputs.VerticalVoxelSize,
            startInputs.MaxSurfaceHeightDifference,
            startInputs.MaxWalkableHeight);
        AgentDataContainer = new AgentDataContainer(this);
        AgentRemovingSystem = new AgentRemovingSystem(this);
        PathDataContainer = new PathDataContainer(this);
        RequestAccumulator = new RequestAccumulator(this);
        PathConstructionPipeline = new PathConstructionPipeline(this);
        MovementManager = new MovementManager(AgentDataContainer, this);
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
    internal uint GetFieldState()
    {
        return _navigationUpdater.GetFieldState();
    }
    internal NativeArray<UnsafeList<HashTile>> GetSpatialHashGridArray()
    {
        return MovementManager.HashGridArray;
    }
    internal NativeArray<int> GetNormalToHashed()
    {
        return MovementManager.NormalToHashed;
    }
    internal NativeArray<AgentMovementData> GetAgentMovementData()
    {
        return MovementManager.AgentMovementDataList;
    }
    internal UnsafeListReadOnly<byte>[] GetAllCostFieldCostsAsUnsafeListReadonly()
    {
        return FieldDataContainer.GetAllCostFieldCostsAsUnsafeListReadonly();
    }
}