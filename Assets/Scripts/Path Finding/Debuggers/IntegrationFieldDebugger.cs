using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IntegrationFieldDebugger : MonoBehaviour
{
    [SerializeField] bool _debugBFS;
    [SerializeField] bool _debugPortalSequence;
    [SerializeField] bool _debugPickedSectors;
    [SerializeField] bool _debugIntegrationField;
    [SerializeField] bool _debugFlowField;

    [SerializeField] PathfindingManager _pathfindingManager;

    // Start is called before the first frame update


    // Update is called once per frame

    private void OnDrawGizmos()
    {
        PathProducer pathProducer = _pathfindingManager.PathProducer;
        if (_debugBFS && pathProducer != null) { pathProducer.DebugBFS(); }
        if (_debugPortalSequence && pathProducer != null) { pathProducer.DebugPortalSequence(); }
        if (_debugPickedSectors && pathProducer != null) { pathProducer.DebugPickedSectors(); }
        if (_debugIntegrationField && pathProducer != null) { pathProducer.DebugIntegrationField(_pathfindingManager.TilePositions); }
        if (_debugFlowField && pathProducer != null) { pathProducer.DebugFlowField(_pathfindingManager.TilePositions); }
    }
}
