#if (UNITY_EDITOR) 

using UnityEngine;

public class EditorDebuggingController : MonoBehaviour
{
    [SerializeField] PathfindingManager _pathfindingManager;
    [SerializeField] AgentSelectionController _agentSelectionController;
    [Header("Field Debugger")]
    [SerializeField] CostFieldOffset _costFieldOffset;
    [SerializeField] bool _sectors;
    [SerializeField] bool _windows;
    [SerializeField] bool _sectorWindows;
    [SerializeField] bool _windowSectors;
    [SerializeField] bool _portals;
    [SerializeField] bool _portalsOnSector;
    [SerializeField] bool _costsToPortal;
    [SerializeField] bool _AStar;
    [Header("PathDebugger")]
    [SerializeField] bool _debugPortalTraversalMarks;
    [SerializeField] bool _debugPortalSequence;
    [SerializeField] bool _debugPickedSectors;
    [SerializeField] bool _debugIntegrationField;
    [SerializeField] bool _debugLOSPass;
    [SerializeField] bool _debugLOSBlocks;
    [SerializeField] bool _debugFlowField;
    [Header("Agent Debugger")]
    [SerializeField] bool _debugAgentDirections;


    //debuggers
    EditorSectorDebugger _sectorDebugger;
    EditorWindowDebugger _windowDebugger;
    EditorSectorGraphDebugger _sectorGraphDebugger;
    EditorPortalDebugger _portalDebugger;
    EditorAStarDebugger _aStarDebugger;
    EditorPathDebugger _pathDebugger;
    EditorAgentDirectionDebugger _agentDirectionDebugger;
    private void Start()
    {
        _sectorDebugger = new EditorSectorDebugger(_pathfindingManager);
        _windowDebugger = new EditorWindowDebugger(_pathfindingManager);
        _sectorGraphDebugger = new EditorSectorGraphDebugger(_pathfindingManager);
        _portalDebugger = new EditorPortalDebugger(_pathfindingManager);
        _aStarDebugger = new EditorAStarDebugger(_pathfindingManager);
        _agentDirectionDebugger = new EditorAgentDirectionDebugger(_pathfindingManager);
    }
    private void Update()
    {
        if(_pathDebugger == null) { _pathDebugger = new EditorPathDebugger(_pathfindingManager); }
    }
    private void OnDrawGizmos()
    {

        if (_sectors && _sectorDebugger != null) { _sectorDebugger.DebugSectors((int) _costFieldOffset); }
        if( _windows && _windowDebugger != null) { _windowDebugger.DebugWindows((int) _costFieldOffset); }
        if(_sectorWindows && _sectorGraphDebugger != null) { _sectorGraphDebugger.DebugSectorToWindow((int) _costFieldOffset); }
        if(_windowSectors && _sectorGraphDebugger != null) { _sectorGraphDebugger.DebugWindowToSector((int) _costFieldOffset); }
        if(_portals && _portalDebugger != null) { _portalDebugger.DebugPortals((int) _costFieldOffset); }
        if(_portalsOnSector && _portalDebugger != null) { _portalDebugger.DebugPortalsOnClickedSector((int) _costFieldOffset); }
        if(_costsToPortal && _portalDebugger != null) { _portalDebugger.DebugCostsToClickedPortal((int) _costFieldOffset); }
        if(_AStar && _aStarDebugger != null) { _aStarDebugger.DebugAstarForPortal((int) _costFieldOffset); }

        if(_agentSelectionController == null) { return; }
        FlowFieldAgent _agentToDebug = _agentSelectionController.DebuggableAgent;
        if(_agentToDebug != null)
        {
            if (_debugPortalTraversalMarks && _pathDebugger != null) { _pathDebugger.DebugPortalTraversalMarks(_agentToDebug); }
            if (_debugPortalSequence && _pathDebugger != null) { _pathDebugger.DebugPortalSequence(_agentToDebug); }
            if (_debugPickedSectors && _pathDebugger != null) { _pathDebugger.DebugPickedSectors(_agentToDebug); }
            if (_debugIntegrationField && _pathDebugger != null) { _pathDebugger.DebugIntegrationField(_agentToDebug); }
            if (_debugFlowField && _pathDebugger != null) { _pathDebugger.DebugFlowField(_agentToDebug); }
            if (_debugLOSPass && _pathDebugger != null) { _pathDebugger.LOSPassDebug(_agentToDebug); }
            if (_debugLOSBlocks && _pathDebugger != null) { _pathDebugger.LOSBlockDebug(_agentToDebug); }
        }

        if(_debugAgentDirections && _agentDirectionDebugger != null) { _agentDirectionDebugger.Debug(); }
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
