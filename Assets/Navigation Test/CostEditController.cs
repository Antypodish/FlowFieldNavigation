using System;
using Unity.Mathematics;
using UnityEngine;
using System.Collections.Generic;
using Unity.Collections;
using System.Diagnostics;
using FlowFieldNavigation;
public class CostEditController
{
    GameObject ObstaclePrefab;
    FlowFieldNavigationManager _navigationManager;
    List<GameObject> obstacles;
    public CostEditController(FlowFieldNavigationManager navigationManager, GameObject obstaclePrefab)
    {
        _navigationManager = navigationManager;
        obstacles = new List<GameObject>();
        ObstaclePrefab = obstaclePrefab;
    }
    public void SetObstacle()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, int.MaxValue, 8))
        {
            Vector3 pos = hit.point;
            GameObject obstacleObject = GameObject.Instantiate(ObstaclePrefab);
            obstacleObject.transform.position = pos;
            _navigationManager.Interface.SetObstacle(obstacleObject.GetComponent<FlowFieldDynamicObstacle>());
            obstacles.Add(obstacleObject);
        }
    }
}