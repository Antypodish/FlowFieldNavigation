using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;

namespace Assets.Scripts.Path_Finding.Controllers
{
    internal class CostEditController : MonoBehaviour
    {
        [SerializeField] PathfindingManager _pathfindingManager;
        [SerializeField] byte _cost;
        bool _ePressed = false;
        int2 index1;
        int2 index2;
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                if (_ePressed)
                {
                    float tileSize = _pathfindingManager.TileSize;
                    Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                    RaycastHit hit;
                    if (Physics.Raycast(ray, out hit))
                    {
                        Vector3 pos = hit.point;
                        int2 index = new int2(Mathf.FloorToInt(pos.x / tileSize), Mathf.FloorToInt(pos.z / tileSize));
                        index2 = index;
                        _pathfindingManager.EditCost(index1, index2, _cost);
                        _ePressed = false;
                    }
                }
                else
                {
                    float tileSize = _pathfindingManager.TileSize;
                    Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                    RaycastHit hit;
                    if (Physics.Raycast(ray, out hit))
                    {
                        Vector3 pos = hit.point;
                        int2 index = new int2(Mathf.FloorToInt(pos.x / tileSize), Mathf.FloorToInt(pos.z / tileSize));
                        index1 = index;
                        _ePressed = true;
                    }
                }
            }
        }
    }
}
