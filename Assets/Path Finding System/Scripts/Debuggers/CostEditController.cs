using System;
using Unity.Mathematics;
using UnityEngine;
using System.Collections.Generic;
public class CostEditController
{
    PathfindingManager _pathfindingManager;
    float2 halfSize = new float2(1.5f, 1.5f);
    List<GameObject> obstacles;
    List<int> obstacleKeys;
    public CostEditController(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
        obstacles = new List<GameObject>();
        obstacleKeys = new List<int>();
    }
    public void SetUnwalkable()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, int.MaxValue, 8))
        {
            Vector3 pos = hit.point;
            float2 editPos = new float2(pos.x, pos.z);
            if(_pathfindingManager.SetObstacle(editPos, halfSize, out int obstacleKey))
            {
                GameObject obstacleCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obstacleCube.transform.localScale = new Vector3(halfSize.x, 0f, halfSize.y) * 2 + new Vector3(0, 1f, 0);
                obstacleCube.transform.position = pos;

                obstacles.Add(obstacleCube);
                obstacleKeys.Add(obstacleKey);
                UnityEngine.Debug.Log(obstacleKey);
            }
        }
    }
}