using UnityEngine;

public class PathFindingDebugger : MonoBehaviour
{
    [SerializeField] PathfindingManager _pathfindingManager;
    [SerializeField] CostFieldOffset _costFieldOffset;
    [SerializeField] bool _debugCostField;
    [SerializeField] bool _debugSectors;
    [SerializeField] bool _debugWindows;
    [SerializeField] bool _debugSectorWindows;
    [SerializeField] bool _debugWindowSectors;
    [SerializeField] bool _debugPortals;


    //debuggers
    CostFieldDebugger _costFieldDebugger;
    SectorDebugger _sectorDebugger;
    WindowDebugger _windowDebugger;
    SectorGraphDebugger _sectorGraphDebugger;
    PortalDebugger _portalDebugger;
    private void Start()
    {
        _costFieldDebugger = new CostFieldDebugger(_pathfindingManager);
        _sectorDebugger = new SectorDebugger(_pathfindingManager);
        _windowDebugger = new WindowDebugger(_pathfindingManager);
        _sectorGraphDebugger = new SectorGraphDebugger(_pathfindingManager);
        _portalDebugger = new PortalDebugger(_pathfindingManager);

    }
    private void OnDrawGizmos()
    {
        if (_debugCostField && _costFieldDebugger != null) { _costFieldDebugger.DebugCostFieldWithMesh((int) _costFieldOffset); }
        if (_debugSectors && _sectorDebugger != null) { _sectorDebugger.DebugSectors((int) _costFieldOffset); }
        if( _debugWindows && _windowDebugger != null) { _windowDebugger.DebugWindows((int) _costFieldOffset); }
        if(_debugSectorWindows && _sectorGraphDebugger != null) { _sectorGraphDebugger.DebugSectorToWindow((int) _costFieldOffset); }
        if(_debugWindowSectors && _sectorGraphDebugger != null) { _sectorGraphDebugger.DebugWindowToSector((int) _costFieldOffset); }
        if(_debugPortals && _portalDebugger != null) { _portalDebugger.DebugPortals((int) _costFieldOffset); }
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
