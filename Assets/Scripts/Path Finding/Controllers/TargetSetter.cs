using UnityEngine;

public class TargetSetter : MonoBehaviour
{
    [SerializeField] PathfindingManager _pathfindingManager;
    private void Update()
    {
        if (Input.GetMouseButtonDown(1))
        {
            float tileSize = _pathfindingManager.TileSize;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                Vector3 hitPos = hit.point;
                _pathfindingManager.SetDestination(hitPos);
            }
        }
    }
}
