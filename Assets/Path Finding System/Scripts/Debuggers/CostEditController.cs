using System;
using Unity.Mathematics;
using UnityEngine;
using System.Collections.Generic;
using Unity.Collections;
using System.Diagnostics;
public class CostEditController
{
    PathfindingManager _pathfindingManager;
    float2 halfSize = new float2(1.5f, 1.5f);
    List<GameObject> obstacles;
    NativeList<int> _obstacleKeys;
    public CostEditController(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
        obstacles = new List<GameObject>();
        _obstacleKeys = new NativeList<int>(Allocator.Persistent);
    }
    public void SetUnwalkable()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, int.MaxValue, 8))
        {
            Vector3 pos = hit.point;
            float2 editPos = new float2(pos.x, pos.z);
            NativeArray<ObstacleRequest> obstacleRequestsTemp = new NativeArray<ObstacleRequest>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            for(int i = 0; i < obstacleRequestsTemp.Length; i++)
            {
                obstacleRequestsTemp[i] = new ObstacleRequest()
                {
                    Position = editPos,
                    HalfSize = halfSize,
                };
            }
            _pathfindingManager.SetObstacle(obstacleRequestsTemp, _obstacleKeys);
            obstacleRequestsTemp.Dispose();
            GameObject obstacleCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacleCube.transform.localScale = new Vector3(halfSize.x, 0f, halfSize.y) * 2 + new Vector3(0, 1f, 0);
            obstacleCube.transform.position = pos;

            obstacles.Add(obstacleCube);

        }
    }
}