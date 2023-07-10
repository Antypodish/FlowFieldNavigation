using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

public class PathfindingUpdateRoutine
{
    PathfindingManager _pathfindingManager;
    AgentDirectionCalculator _dirCalculator;

    public PathfindingUpdateRoutine(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
        _dirCalculator = new AgentDirectionCalculator(_pathfindingManager.AgentDataContainer, _pathfindingManager);
    }

    public void Update(float deltaTime)
    {
        _dirCalculator.CalculateDirections();
    }
    public void LateUpdate(float deltaTime)
    {

    }
}
