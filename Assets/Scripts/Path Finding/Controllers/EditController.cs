using UnityEngine;

public class EditController : MonoBehaviour
{
    [SerializeField] PathfindingManager _pathfindingManager;
    [SerializeField] EditValue _editValue;
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
                    _pathfindingManager.EditCost(_firstClicked, _secondClicked, (byte) _editValue);
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
    private enum EditValue : byte
    {
        Walkable = 1,
        Unwalkable = byte.MaxValue
    };
}
