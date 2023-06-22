using Unity.Collections;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

public class TargetSetter : MonoBehaviour
{
    NativeList<Vector3> _sources;
    NativeList<Vector3> _sourcesForEachSector;
    Vector3 _target;
    [SerializeField] PathfindingManager _pathfindingManager;
    [SerializeField] bool _placeSourceForEachSector;
    private void Start()
    {
        _sources = new NativeList<Vector3>(Allocator.Persistent);
        _sourcesForEachSector = new NativeList<Vector3>(Allocator.Persistent);
        NativeArray<Vector3> tilePositions = _pathfindingManager.TilePositions;
        float tileSize = _pathfindingManager.TileSize;
        float sectorColAmount = _pathfindingManager.SectorTileAmount;
        int sectorMatrixCol = _pathfindingManager.SectorMatrixColAmount;
        int sectorMatrixRow = _pathfindingManager.SectorMatrixRowAmount;
        for(int i = 0; i < sectorMatrixRow; i++)
        {
            for(int j = 0; j < sectorMatrixCol; j++)
            {
                Vector3 pos = new Vector3(j * sectorColAmount * tileSize + tileSize * 2, 0f, i * sectorColAmount * tileSize + tileSize * 2);
                _sourcesForEachSector.Add(pos);
            }
        }
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
                
                NativeArray<Vector3> sorucesCopy ;
                if (_placeSourceForEachSector)
                {
                    sorucesCopy = new NativeArray<Vector3>(_sourcesForEachSector, Allocator.Persistent);
                }
                else
                {
                    sorucesCopy = new NativeArray<Vector3>(_sources, Allocator.Persistent);
                }
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
            Gizmos.DrawSphere(_sources[i], 0.2f);
        }

        Gizmos.color = Color.white;
        Gizmos.DrawSphere(_target, 0.2f);
    }
}
