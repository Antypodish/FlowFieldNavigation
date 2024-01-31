#if (UNITY_EDITOR) 

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
    [SerializeField] bool _costField;
    [SerializeField] bool _sectors;
    [SerializeField] bool _windows;
    [SerializeField] bool _portals;
    [SerializeField] bool _portalIslands;
    [SerializeField] bool _sectorIslands;
    [SerializeField] bool _portalErrorDetection;
    [Header("Height Mesh Debugger")]
    [SerializeField] int _meshGridIndex;
    [SerializeField] bool _debugHeightMesh;
    [SerializeField] bool _debugTrianglesAtClickedTile;
    [SerializeField] bool _debugGridBorders;
    [Header("PathDebugger")]
    [SerializeField] bool _debugDestination;
    [SerializeField] bool _debugPortalTraversalMarks;
    [SerializeField] bool _debugPortalTargetNeighbours;
    [SerializeField] bool _debugPortalSequence;
    [SerializeField] bool _debugPickedSectors;
    [SerializeField] bool _debugIntegrationField;
    [SerializeField] bool _debugLOSBlocks;
    [SerializeField] bool _debugFlowField;
    [SerializeField] bool _debugActiveWaveFronts;
    [SerializeField] bool _debugDynamicAreaIntegration;
    [SerializeField] bool _debugDynamicAreaFlow;
    [Header("Agent Debugger")]
    [SerializeField] bool _debugAgentDirections;
    [SerializeField] bool _debugAgentsHoldingGround;
    [SerializeField] bool _debugAgentSeperationRadius;
    [SerializeField] bool _debugAvoidanceDirections;
    [SerializeField] bool _debugAgentPathIndicies;
    [Header("Spatial Hash Grid Debugger")]
    [SerializeField] int _gridIndex;
    [SerializeField] bool _debugSpatialHashGrid;
    [SerializeField] bool _debugBorders;
    [SerializeField] bool _debugAroundAgent;
    [SerializeField] float _checkRange;

    //debuggers
    EditorSectorDebugger _sectorDebugger;
    EditorWindowDebugger _windowDebugger;
    EditorPortalDebugger _portalDebugger;
    EditorPathDebugger _pathDebugger;
    EditorAgentDirectionDebugger _agentDirectionDebugger;
    EditorCostFieldDebugger _costFieldDebugger;
    EditorHoldGroundDebugger _holdGroundDebugger;
    EditorAgentRadiusDebugger _agentRadiusDebugger;
    EditorAvoidanceDirectionDebugger _avoidanceDirectionDebugger;
    EditorSpatialHashGridDebugger _spatialHashGridDebugger;
    EditorIslandDebugger _islandDebugger;
    EditorHeightMeshDebugger _heightMeshDebugger;
    EditorAgentPathIndexDebugger _agentPathIndexDebugger;

    Camera _cameraToRender;
    private void Start()
    {
        _cameraToRender = GetComponent<Camera>();
        _cameraToOverlayOnTopOf.GetComponent<UniversalAdditionalCameraData>().cameraStack.Add(_cameraToRender);

        _sectorDebugger = new EditorSectorDebugger(_pathfindingManager);
        _windowDebugger = new EditorWindowDebugger(_pathfindingManager);
        _portalDebugger = new EditorPortalDebugger(_pathfindingManager);
        _agentDirectionDebugger = new EditorAgentDirectionDebugger(_pathfindingManager);
        _costFieldDebugger = new EditorCostFieldDebugger(_pathfindingManager);
        _holdGroundDebugger = new EditorHoldGroundDebugger(_pathfindingManager);
        _agentRadiusDebugger = new EditorAgentRadiusDebugger(_pathfindingManager);
        _avoidanceDirectionDebugger = new EditorAvoidanceDirectionDebugger(_pathfindingManager);
        _spatialHashGridDebugger = new EditorSpatialHashGridDebugger(_pathfindingManager);
        _islandDebugger = new EditorIslandDebugger(_pathfindingManager);
        _heightMeshDebugger = new EditorHeightMeshDebugger(_pathfindingManager);
        _agentPathIndexDebugger = new EditorAgentPathIndexDebugger(_pathfindingManager);

#if UNITY_STANDALONE && !UNITY_EDITOR
_debuggingEnabled = false;
#endif
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
        if(Camera.current != _cameraToRender) { return; }

        if (_sectors && _sectorDebugger != null) { _sectorDebugger.DebugSectors(_costFieldOffset); }
        if( _windows && _windowDebugger != null) { _windowDebugger.DebugWindows(_costFieldOffset); }
        if(_portals && _portalDebugger != null) { _portalDebugger.DebugPortals(_costFieldOffset); }
        if(_costField && _costFieldDebugger != null) { _costFieldDebugger.DebugCostFieldWithMesh(_costFieldOffset); }
        if(_portalIslands && _islandDebugger != null) { _islandDebugger.DebugPortalIslands(_costFieldOffset); }
        if(_sectorIslands && _islandDebugger != null) { _islandDebugger.DebugTileIslands(_costFieldOffset); }
        if(_debugHeightMesh && _heightMeshDebugger != null) { _heightMeshDebugger.DebugHeightMapMesh(); }
        if(_debugTrianglesAtClickedTile && _heightMeshDebugger != null) { _heightMeshDebugger.DebugTrianglesAtTile(); }
        if(_debugGridBorders && _heightMeshDebugger != null) { _heightMeshDebugger.DebugBorders(_meshGridIndex); }

        if(AgentToDebug != null)
        {
            if (_debugPortalTraversalMarks && _pathDebugger != null) { _pathDebugger.DebugPortalTraversalMarks(AgentToDebug); }
            if (_debugPortalSequence && _pathDebugger != null) { _pathDebugger.DebugPortalSequence(AgentToDebug); }
            if (_debugPickedSectors && _pathDebugger != null) { _pathDebugger.DebugPickedSectors(AgentToDebug); }
            if (_debugIntegrationField && _pathDebugger != null) { _pathDebugger.DebugIntegrationField(AgentToDebug); }
            if (_debugFlowField && _pathDebugger != null) { _pathDebugger.DebugFlowField(AgentToDebug); }
            if (_debugLOSBlocks && _pathDebugger != null) { _pathDebugger.LOSBlockDebug(AgentToDebug); }
            if(_debugActiveWaveFronts && _pathDebugger != null) { _pathDebugger.DebugActiveWaveFronts(AgentToDebug); }
            if(_debugPortalTargetNeighbours && _pathDebugger != null) { _pathDebugger.DebugTargetNeighbourPortals(AgentToDebug); }
            if(_debugDestination && _pathDebugger != null) { _pathDebugger.DebugDestination(AgentToDebug); }
            if (_debugDynamicAreaIntegration && _pathDebugger != null) { _pathDebugger.DebugDynamicAreaIntegration(AgentToDebug); }
            if (_debugDynamicAreaFlow && _pathDebugger != null) { _pathDebugger.DebugDynamicAreaFlow(AgentToDebug); }
            if (_debugAroundAgent && _spatialHashGridDebugger != null) { _spatialHashGridDebugger.DebugAgent(AgentToDebug, _gridIndex, _checkRange); }
            if (_debugAgentPathIndicies && _agentPathIndexDebugger != null) { _agentPathIndexDebugger.Debug(AgentToDebug); }
        }

        if (_debugAgentDirections && _agentDirectionDebugger != null) { _agentDirectionDebugger.Debug(); }
        if(_debugAgentsHoldingGround && _holdGroundDebugger != null) { _holdGroundDebugger.Debug(); }
        if(_debugAgentSeperationRadius && _agentRadiusDebugger != null) { _agentRadiusDebugger.DebugSeperationRadius(); }
        if (_debugAvoidanceDirections && _avoidanceDirectionDebugger != null) { _avoidanceDirectionDebugger.Debug(); }
        if(_debugSpatialHashGrid && _spatialHashGridDebugger != null) { _spatialHashGridDebugger.Debug(_gridIndex); }
        if(_debugBorders && _spatialHashGridDebugger != null) { _spatialHashGridDebugger.DebugBorders(_gridIndex); }
    }

    void HandleNewPos()
    {
        transform.position = _cameraToOverlayOnTopOf.transform.position;
        transform.rotation = _cameraToOverlayOnTopOf.transform.rotation;
        transform.localScale = _cameraToOverlayOnTopOf.transform.localScale;
    }
}
#endif
