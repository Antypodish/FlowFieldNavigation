

using UnityEngine;
using UnityEngine.Rendering.Universal;

public class EditorDebuggingController : MonoBehaviour
{
    [SerializeField] PathfindingManager _pathfindingManager;
    [SerializeField] Camera _cameraToOverlayOnTopOf;
    [SerializeField] bool _debuggingEnabled;
    [HideInInspector] public FlowFieldAgent AgentToDebug;
    [Header("Field Debugger")]
    [SerializeField] int _costFieldOffset;
    [SerializeField] bool _debugTiles;
    [SerializeField] bool _costField;
    [SerializeField] bool _sectors;
    [SerializeField] bool _portals;
    [SerializeField] bool _islands;
    [SerializeField] bool _portalErrorDetection;/*
    [Header("Navigation Volume Debugger")]
    [SerializeField] bool _debugVolumeBounds;
    [SerializeField] bool _debugSectorBounds;
    [SerializeField] bool _debugDetectedSectors;
    [SerializeField] bool _debugNavigationSurfaceVolume;
    [SerializeField] bool _debugHighestSurfaceVoxels;*/
    [Header("Height Mesh Debugger")]
    [SerializeField] int _meshGridIndex;
    [SerializeField] bool _debugHeightMesh;
    [SerializeField] bool _debugGridBorders;
    [Header("PathDebugger")]
    [SerializeField] bool _debugDestination;
    [SerializeField] bool _debugPortalTraversalMarks;
    [SerializeField] bool _debugPortalTargetNeighbours;
    [SerializeField] bool _debugPortalSequence;
    [SerializeField] bool _debugPickedSectors;
    [SerializeField] bool _debugIntegrationField;
    [SerializeField] bool _debugFlowField;
    [SerializeField] bool _debugDynamicAreaIntegration;
    [SerializeField] bool _debugDynamicAreaFlow;
    [Header("Agent Debugger")]
    [SerializeField] bool _debugAgentDirections;
    [SerializeField] bool _debugAgentsHoldingGround;
    [SerializeField] bool _debugAgentSeperationRadius;
    [SerializeField] bool _debugAvoidanceDirections;
    [Header("Spatial Hash Grid Debugger")]
    [SerializeField] int _gridIndex;
    [SerializeField] bool _debugSpatialHashGrid;
    [SerializeField] bool _debugBorders;
    [SerializeField] bool _debugAroundAgent;
#if (UNITY_EDITOR)
    //debuggers
    EditorSectorDebugger _sectorDebugger;
    EditorPortalDebugger _portalDebugger;
    EditorPathDebugger _pathDebugger;
    EditorAgentDirectionDebugger _agentDirectionDebugger;
    EditorCostFieldDebugger _costFieldDebugger;
    EditorHoldGroundDebugger _holdGroundDebugger;
    EditorAgentRadiusDebugger _agentRadiusDebugger;
    EditorAvoidanceDirectionDebugger _avoidanceDirectionDebugger;
    EditorSpatialHashGridDebugger _spatialHashGridDebugger;
    EditorTileIslandDebugger _islandDebugger;
    EditorHeightMeshDebugger _heightMeshDebugger;
    EditorTileDebugger _tileDebugger;
    EditorNavigationVolumeDebugger _navVolDebugger;

    Camera _overlayCamera;
    PortalHeightBuilder _portalHeightBuilder;
    TileCenterHeightBuilder _tileCenterHeightBuilder;
    SectorCornerHeightBuilder _sectorCornerHeightBuilder;
    private void Start()
    {
        _overlayCamera = GetComponent<Camera>();
        _cameraToOverlayOnTopOf.GetComponent<UniversalAdditionalCameraData>().cameraStack.Add(_overlayCamera);

        _sectorDebugger = new EditorSectorDebugger(_pathfindingManager);
        _portalDebugger = new EditorPortalDebugger(_pathfindingManager);
        _agentDirectionDebugger = new EditorAgentDirectionDebugger(_pathfindingManager);
        _costFieldDebugger = new EditorCostFieldDebugger(_pathfindingManager);
        _holdGroundDebugger = new EditorHoldGroundDebugger(_pathfindingManager);
        _agentRadiusDebugger = new EditorAgentRadiusDebugger(_pathfindingManager);
        _avoidanceDirectionDebugger = new EditorAvoidanceDirectionDebugger(_pathfindingManager);
        _spatialHashGridDebugger = new EditorSpatialHashGridDebugger(_pathfindingManager);
        _islandDebugger = new EditorTileIslandDebugger(_pathfindingManager);
        _heightMeshDebugger = new EditorHeightMeshDebugger(_pathfindingManager);
        _tileDebugger = new EditorTileDebugger(_pathfindingManager);
        _portalHeightBuilder = new PortalHeightBuilder(_pathfindingManager);
        _tileCenterHeightBuilder = new TileCenterHeightBuilder(_pathfindingManager);
        _sectorCornerHeightBuilder = new SectorCornerHeightBuilder(_pathfindingManager);
        _navVolDebugger = new EditorNavigationVolumeDebugger(_pathfindingManager);
    }
    private void Update()
    {
        if(_pathDebugger == null) { _pathDebugger = new EditorPathDebugger(_pathfindingManager); }
        HandleNewPos();
    }
    private void OnDrawGizmos()
    {
        if (!_pathfindingManager.SimulationStarted) { return; }
        FlowFieldUtilities.DebugMode = _debuggingEnabled;
        if (!_debuggingEnabled) { return; }
        _costFieldOffset = Mathf.Min(FlowFieldUtilities.MaxCostFieldOffset, _costFieldOffset);

        if (_debugTiles) { _tileDebugger.Debug(); }
        if (_costField) { _costFieldDebugger.DebugCostFieldWithMesh(_costFieldOffset); }
        if (_sectors) { _sectorDebugger.DebugSectors(); }
        if (_portals) { _portalDebugger.DebugPortals(_costFieldOffset); }
        if (_islands) { _islandDebugger.DebugTileIslands(_costFieldOffset); }
        if (_debugHeightMesh) { _heightMeshDebugger.DebugHeightMapMesh(); }
        if (_debugGridBorders) { _heightMeshDebugger.DebugBorders(_meshGridIndex); }
        if (_debugBorders) { _spatialHashGridDebugger.DebugBorders(_gridIndex); }
        if (_debugAgentDirections) { _agentDirectionDebugger.Debug(); }
        if (_debugAgentsHoldingGround) { _holdGroundDebugger.Debug(); }
        if (_debugAgentSeperationRadius) { _agentRadiusDebugger.DebugSeperationRadius(); }
        if (_debugAvoidanceDirections) { _avoidanceDirectionDebugger.Debug(); }
        if (_debugSpatialHashGrid) { _spatialHashGridDebugger.Debug(_gridIndex); }/*
        if (_debugVolumeBounds) { _navVolDebugger.DebugVolumeBoundaries(); }
        if (_debugSectorBounds) { _navVolDebugger.DebugVolumeSectorBounds(); }
        if (_debugDetectedSectors) { _navVolDebugger.DebugVolumeDetectedSectors(); }
        if (_debugNavigationSurfaceVolume) { _navVolDebugger.DebugNavigationSurfaceVolume(); }
        if (_debugHighestSurfaceVoxels) { _navVolDebugger.DebugHighestVoxels(); }*/

        if (AgentToDebug != null)
        {
            if (_debugAroundAgent) { _spatialHashGridDebugger.DebugAgent(AgentToDebug, _gridIndex); }
            if (_debugPortalTraversalMarks) { _pathDebugger.DebugPortalTraversalMarks(AgentToDebug, _portalHeightBuilder.GetPortalHeights(_costFieldOffset)); }
            if (_debugPortalTargetNeighbours) { _pathDebugger.DebugTargetNeighbourPortals(AgentToDebug, _portalHeightBuilder.GetPortalHeights(_costFieldOffset)); }
            if (_debugPortalSequence) { _pathDebugger.DebugPortalSequence(AgentToDebug, _portalHeightBuilder.GetPortalHeights(_costFieldOffset)); }
            if (_debugIntegrationField) { _pathDebugger.DebugIntegrationField(AgentToDebug, _tileCenterHeightBuilder.GetTileCenterHeights()); }
            if (_debugFlowField) { _pathDebugger.DebugFlowField(AgentToDebug, _tileCenterHeightBuilder.GetTileCenterHeights()); }
            if (_debugDestination) { _pathDebugger.DebugDestination(AgentToDebug); }
            if (_debugDynamicAreaIntegration) { _pathDebugger.DebugDynamicAreaIntegration(AgentToDebug, _tileCenterHeightBuilder.GetTileCenterHeights()); }
            if (_debugDynamicAreaFlow) { _pathDebugger.DebugDynamicAreaFlow(AgentToDebug, _tileCenterHeightBuilder.GetTileCenterHeights()); }
            if (_debugPickedSectors) { _pathDebugger.DebugPickedSectors(AgentToDebug, _sectorCornerHeightBuilder.GetSectorCornerHeights()); }
        }

    }

    void HandleNewPos()
    {
        transform.position = _cameraToOverlayOnTopOf.transform.position;
        transform.rotation = _cameraToOverlayOnTopOf.transform.rotation;
        transform.localScale = _cameraToOverlayOnTopOf.transform.localScale;
    }
#endif
}