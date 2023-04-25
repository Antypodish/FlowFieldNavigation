using System;
using System.Xml.XPath;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

public class FlowFieldDebugger : MonoBehaviour
{
    [SerializeField] PathfindingManager _pathfindingManager;
    [SerializeField] bool _debugCostField;
    [SerializeField] CostFieldOffset _costFieldOffset;
    [SerializeField] bool _debugTilePositions;

    //debuggers
    TilePositionDebugger _tilePositionDebugger;
    CostFieldDebugger _costFieldDebugger;
    private void Start()
    {
        _tilePositionDebugger = new TilePositionDebugger(_pathfindingManager);
        _costFieldDebugger = new CostFieldDebugger(_pathfindingManager);
    }
    private void OnDrawGizmos()
    {
        if (_debugTilePositions && _tilePositionDebugger != null) { _tilePositionDebugger.DebugTilePositions(); }
        if (_debugCostField && _costFieldDebugger != null) { _costFieldDebugger.DebugCostField(((int) _costFieldOffset)); }

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
