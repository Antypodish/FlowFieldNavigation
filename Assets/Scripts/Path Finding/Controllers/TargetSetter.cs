using Unity.Collections;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

public class TargetSetter : MonoBehaviour
{
    NativeList<Vector3> _sources;
    Vector3 _target;
    [SerializeField] PathfindingManager _pathfindingManager;
    private void Start()
    {
        _sources = new NativeList<Vector3>(Allocator.Persistent);
    }
    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            float tileSize = _pathfindingManager.TileSize;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                _sources.Add(hit.point);
            }
        }
        if (Input.GetMouseButton(1))
        {
            float tileSize = _pathfindingManager.TileSize;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                Vector3 hitPos = hit.point;
                _target = hitPos;
                NativeArray<Vector3> sorucesCopy = new NativeArray<Vector3>(_sources, Allocator.Persistent);
                _pathfindingManager.SetDestination(sorucesCopy, _target);
            }
        }
        if (Input.GetMouseButtonDown(2))
        {
            _sources.Clear();
        }
    }

    private void OnDrawGizmos()
    {
        if (!_sources.IsCreated) { return; } 
        Gizmos.color = Color.red;
        for(int i = 0; i < _sources.Length; i++)
        {
            Gizmos.DrawSphere(_sources[i], 0.4f);
        }

        Gizmos.color = Color.white;
        Gizmos.DrawSphere(_target, 0.4f);
    }
}
