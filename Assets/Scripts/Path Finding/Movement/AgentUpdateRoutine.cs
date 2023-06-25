using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

public class AgentUpdateRoutine
{
    PathfindingManager _pathfindingManager;
    AgentDirectionCalculator _dirCalculator;

    public AgentUpdateRoutine(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
        _dirCalculator = new AgentDirectionCalculator(_pathfindingManager.AgentDataContainer);
    }

    public void Update(float deltaTime)
    {
        _dirCalculator.CalculateDirections();
    }
}
