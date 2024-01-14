#if (UNITY_EDITOR) 

using UnityEngine;

public class EditorDebuggingController : MonoBehaviour
{
    [SerializeField] PathfindingManager _pathfindingManager;
    [SerializeField] bool _debuggingEnabled;
    [HideInInspector] public FlowFieldAgent AgentToDebug;
    [Header("Field Debugger")]
    [SerializeField] CostFieldOffset _costFieldOffset;
    [SerializeField] bool _costField;
    [SerializeField] bool _sectors;
    [SerializeField] bool _windows;
    [SerializeField] bool _sectorWindows;
    [SerializeField] bool _windowSectors;
    [SerializeField] bool _portals;
    [SerializeField] bool _portalsOnSector;
    [SerializeField] bool _costsToPortal;
    [SerializeField] bool _portalIslands;
    [SerializeField] bool _sectorIslands;
    [SerializeField] bool _portalErrorDetection;
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
    [SerializeField] bool _debugAgentWaypoint;
    [SerializeField] bool _debugAgentsHoldingGround;
    [SerializeField] bool _debugAgentSeperationRadius;
    [SerializeField] bool _debugAvoidanceDirections;
    [Header("Spatial Hash Grid Debugger")]
    [SerializeField] int _gridIndex;
    [SerializeField] bool _debugSpatialHashGrid;
    [SerializeField] bool _debugBorders;
    [SerializeField] bool _debugAroundAgent;
    [SerializeField] float _checkRange;

    //debuggers
    EditorSectorDebugger _sectorDebugger;
    EditorWindowDebugger _windowDebugger;
    EditorSectorGraphDebugger _sectorGraphDebugger;
    EditorPortalDebugger _portalDebugger;
    EditorPathDebugger _pathDebugger;
    EditorAgentDirectionDebugger _agentDirectionDebugger;
    EditorAgentWaypointDebugger _agentWaypointDebugger;
    EditorCostFieldDebugger _costFieldDebugger;
    EditorHoldGroundDebugger _holdGroundDebugger;
    EditorAgentRadiusDebugger _agentRadiusDebugger;
    EditorAvoidanceDirectionDebugger _avoidanceDirectionDebugger;
    EditorSpatialHashGridDebugger _spatialHashGridDebugger;
    EditorIslandDebugger _islandDebugger;
    EditorSectorPortalErrorDetector _portalErrorDetector;
    private void Start()
    {
        _sectorDebugger = new EditorSectorDebugger(_pathfindingManager);
        _windowDebugger = new EditorWindowDebugger(_pathfindingManager);
        _sectorGraphDebugger = new EditorSectorGraphDebugger(_pathfindingManager);
        _portalDebugger = new EditorPortalDebugger(_pathfindingManager);
        _agentDirectionDebugger = new EditorAgentDirectionDebugger(_pathfindingManager);
        _agentWaypointDebugger = new EditorAgentWaypointDebugger(_pathfindingManager);
        _costFieldDebugger = new EditorCostFieldDebugger(_pathfindingManager);
        _holdGroundDebugger = new EditorHoldGroundDebugger(_pathfindingManager);
        _agentRadiusDebugger = new EditorAgentRadiusDebugger(_pathfindingManager);
        _avoidanceDirectionDebugger = new EditorAvoidanceDirectionDebugger(_pathfindingManager);
        _spatialHashGridDebugger = new EditorSpatialHashGridDebugger(_pathfindingManager);
        _islandDebugger = new EditorIslandDebugger(_pathfindingManager);
        _portalErrorDetector = new EditorSectorPortalErrorDetector(_pathfindingManager);
    }
    private void Update()
    {
        if(_pathDebugger == null) { _pathDebugger = new EditorPathDebugger(_pathfindingManager); }
    }
    private void OnDrawGizmos()
    {
        if (!_pathfindingManager.SimulationStarted) { return; }
        FlowFieldUtilities.DebugMode = _debuggingEnabled;
        if (!_debuggingEnabled) { return; }

        if (_sectors && _sectorDebugger != null) { _sectorDebugger.DebugSectors((int) _costFieldOffset); }
        if( _windows && _windowDebugger != null) { _windowDebugger.DebugWindows((int) _costFieldOffset); }
        if(_sectorWindows && _sectorGraphDebugger != null) { _sectorGraphDebugger.DebugSectorToWindow((int) _costFieldOffset); }
        if(_windowSectors && _sectorGraphDebugger != null) { _sectorGraphDebugger.DebugWindowToSector((int) _costFieldOffset); }
        if(_portals && _portalDebugger != null) { _portalDebugger.DebugPortals((int) _costFieldOffset); }
        if(_portalsOnSector && _portalDebugger != null) { _portalDebugger.DebugPortalsOnClickedSector((int) _costFieldOffset); }
        if(_costsToPortal && _portalDebugger != null) { _portalDebugger.DebugCostsToClickedPortal((int) _costFieldOffset); }
        if(_costField && _costFieldDebugger != null) { _costFieldDebugger.DebugCostFieldWithMesh((int) _costFieldOffset); }
        if(_portalIslands && _islandDebugger != null) { _islandDebugger.DebugPortalIslands((int) _costFieldOffset); }
        if(_sectorIslands && _islandDebugger != null) { _islandDebugger.DebugTileIslands((int) _costFieldOffset); }
        if(_portalErrorDetection && _portalErrorDetector != null) { _portalErrorDetector.Debug((int) _costFieldOffset); }

        if(AgentToDebug == null) { return; }
        FlowFieldAgent _agentToDebug = AgentToDebug;
        if(_agentToDebug != null)
        {
            if (_debugPortalTraversalMarks && _pathDebugger != null) { _pathDebugger.DebugPortalTraversalMarks(_agentToDebug); }
            if (_debugPortalSequence && _pathDebugger != null) { _pathDebugger.DebugPortalSequence(_agentToDebug); }
            if (_debugPickedSectors && _pathDebugger != null) { _pathDebugger.DebugPickedSectors(_agentToDebug); }
            if (_debugIntegrationField && _pathDebugger != null) { _pathDebugger.DebugIntegrationField(_agentToDebug); }
            if (_debugFlowField && _pathDebugger != null) { _pathDebugger.DebugFlowField(_agentToDebug); }
            if (_debugLOSBlocks && _pathDebugger != null) { _pathDebugger.LOSBlockDebug(_agentToDebug); }
            if(_debugAgentWaypoint && _agentWaypointDebugger!= null) { _agentWaypointDebugger.Debug(_agentToDebug); }
            if(_debugActiveWaveFronts && _pathDebugger != null) { _pathDebugger.DebugActiveWaveFronts(_agentToDebug); }
            if(_debugPortalTargetNeighbours && _pathDebugger != null) { _pathDebugger.DebugTargetNeighbourPortals(_agentToDebug); }
            if(_debugDestination && _pathDebugger != null) { _pathDebugger.DebugDestination(_agentToDebug); }
            if (_debugDynamicAreaIntegration && _pathDebugger != null) { _pathDebugger.DebugDynamicAreaIntegration(_agentToDebug); }
            if (_debugDynamicAreaFlow && _pathDebugger != null) { _pathDebugger.DebugDynamicAreaFlow(_agentToDebug); }
            if (_debugAroundAgent && _spatialHashGridDebugger != null) { _spatialHashGridDebugger.DebugAgent(_agentToDebug, _gridIndex, _checkRange); }
        }

        if (_debugAgentDirections && _agentDirectionDebugger != null) { _agentDirectionDebugger.Debug(); }
        if(_debugAgentsHoldingGround && _holdGroundDebugger != null) { _holdGroundDebugger.Debug(); }
        if(_debugAgentSeperationRadius && _agentRadiusDebugger != null) { _agentRadiusDebugger.DebugSeperationRadius(); }
        if (_debugAvoidanceDirections && _avoidanceDirectionDebugger != null) { _avoidanceDirectionDebugger.Debug(); }
        if(_debugSpatialHashGrid && _spatialHashGridDebugger != null) { _spatialHashGridDebugger.Debug(_gridIndex); }
        if(_debugBorders && _spatialHashGridDebugger != null) { _spatialHashGridDebugger.DebugBorders(_gridIndex); }
    }

    enum CostFieldOffset : byte
    {
        Zero = 0,
        One = 1,
        Two = 2,
        Three = 3,
        Four = 4,
        Five = 5
    }

}
#endif
