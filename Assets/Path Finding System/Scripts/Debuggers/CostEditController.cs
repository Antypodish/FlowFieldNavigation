using System;
using Unity.Mathematics;
using UnityEngine;

public class CostEditController
{
    PathfindingManager _pathfindingManager;
    bool _settingUnwalkable = false;
    bool _settingWalkable = false;
    int2 index1;
    int2 index2;

    public CostEditController(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
    }
    public void SetUnwalkable()
    {
        _settingWalkable = false;
        if (_settingUnwalkable)
        {
            float tileSize = _pathfindingManager.TileSize;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                Vector3 pos = hit.point;
                int2 index = new int2(Mathf.FloorToInt(pos.x / tileSize), Mathf.FloorToInt(pos.z / tileSize));
                index2 = index;
                _pathfindingManager.EditCost(index1, index2, byte.MaxValue);
                _settingUnwalkable = false;
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
                _settingUnwalkable = true;
            }
        }
    }
    public void SetWalkable()
    {
        _settingUnwalkable = false;
        if (_settingWalkable)
        {
            float tileSize = _pathfindingManager.TileSize;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                Vector3 pos = hit.point;
                int2 index = new int2(Mathf.FloorToInt(pos.x / tileSize), Mathf.FloorToInt(pos.z / tileSize));
                index2 = index;
                _pathfindingManager.EditCost(index1, index2, 1);
                _settingWalkable = false;
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
                _settingWalkable = true;
            }
        }
    }
}