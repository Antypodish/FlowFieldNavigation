#if (UNITY_EDITOR) 

using UnityEngine;

public class PathFindingDebugger : MonoBehaviour
{
    [SerializeField] PathfindingManager _pathfindingManager;
    [SerializeField] CostFieldOffset _costFieldOffset;
    [Header("Field Debugger")]
    [SerializeField] bool _costField;
    [SerializeField] bool _sectors;
    [SerializeField] bool _windows;
    [SerializeField] bool _sectorWindows;
    [SerializeField] bool _windowSectors;
    [SerializeField] bool _portals;
    [SerializeField] bool _portalsOnSector;
    [SerializeField] bool _costsToPortal;
    [SerializeField] bool _AStar;
    [Header("PathDebugger")]
    [SerializeField] bool _debugBFS;
    [SerializeField] bool _debugPortalSequence;
    [SerializeField] bool _debugPickedSectors;
    [SerializeField] bool _debugIntegrationField;
    [SerializeField] bool _debugFlowField;


    //debuggers
    CostFieldDebugger _costFieldDebugger;
    SectorDebugger _sectorDebugger;
    WindowDebugger _windowDebugger;
    SectorGraphDebugger _sectorGraphDebugger;
    PortalDebugger _portalDebugger;
    AStarDebugger _aStarDebugger;
    private void Start()
    {
        _costFieldDebugger = new CostFieldDebugger(_pathfindingManager);
        _sectorDebugger = new SectorDebugger(_pathfindingManager);
        _windowDebugger = new WindowDebugger(_pathfindingManager);
        _sectorGraphDebugger = new SectorGraphDebugger(_pathfindingManager);
        _portalDebugger = new PortalDebugger(_pathfindingManager);
        _aStarDebugger = new AStarDebugger(_pathfindingManager);
    }
    private void OnDrawGizmos()
    {
        if (_costField && _costFieldDebugger != null) { _costFieldDebugger.DebugCostFieldWithMesh((int)_costFieldOffset); }
        if (_sectors && _sectorDebugger != null) { _sectorDebugger.DebugSectors((int) _costFieldOffset); }
        if( _windows && _windowDebugger != null) { _windowDebugger.DebugWindows((int) _costFieldOffset); }
        if(_sectorWindows && _sectorGraphDebugger != null) { _sectorGraphDebugger.DebugSectorToWindow((int) _costFieldOffset); }
        if(_windowSectors && _sectorGraphDebugger != null) { _sectorGraphDebugger.DebugWindowToSector((int) _costFieldOffset); }
        if(_portals && _portalDebugger != null) { _portalDebugger.DebugPortals((int) _costFieldOffset); }
        if(_portalsOnSector && _portalDebugger != null) { _portalDebugger.DebugPortalsOnClickedSector((int) _costFieldOffset); }
        if(_costsToPortal && _portalDebugger != null) { _portalDebugger.DebugCostsToClickedPortal((int) _costFieldOffset); }
        if(_AStar && _aStarDebugger != null) { _aStarDebugger.DebugAstarForPortal((int) _costFieldOffset); }

        if(_pathfindingManager.PathProducer == null) { return; }
        PathDebugger pathDebugger = _pathfindingManager.PathProducer.GetPathDebugger();
        if (_debugBFS && pathDebugger != null) { pathDebugger.DebugBFS(); }
        if (_debugPortalSequence && pathDebugger != null) { pathDebugger.DebugPortalSequence(); }
        if (_debugPickedSectors && pathDebugger != null) { pathDebugger.DebugPickedSectors(); }
        if (_debugIntegrationField && pathDebugger != null) { pathDebugger.DebugIntegrationField(_pathfindingManager.TilePositions); }
        if (_debugFlowField && pathDebugger != null) { pathDebugger.DebugFlowField(_pathfindingManager.TilePositions); }
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
