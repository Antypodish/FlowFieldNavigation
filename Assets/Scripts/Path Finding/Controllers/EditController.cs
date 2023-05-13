using UnityEngine;

public class EditController : MonoBehaviour
{
    [SerializeField] PathfindingManager _pathfindingManager;

    bool IsAlreadyClicked = false;
    Index2 _firstClicked;
    Index2 _secondClicked;
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
                if (IsAlreadyClicked)
                {
                    _secondClicked = new Index2(Mathf.FloorToInt(hitPos.z / tileSize), Mathf.FloorToInt(hitPos.x / tileSize));
                    _pathfindingManager.SetUnwalkable(_firstClicked, _secondClicked, byte.MaxValue);
                    IsAlreadyClicked = false;
                }
                else
                {
                    _firstClicked = new Index2(Mathf.FloorToInt(hitPos.z / tileSize), Mathf.FloorToInt(hitPos.x / tileSize));
                    IsAlreadyClicked = true;
                }
            }
        }
    }
}
