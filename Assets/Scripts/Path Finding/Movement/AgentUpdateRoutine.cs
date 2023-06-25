using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

public class AgentUpdateRoutine
{
    PathfindingManager _pathfindingManager;
    List<FlowFieldAgent> _agents;
    AgentDirectionCalculator _dirCalculator;

    public AgentUpdateRoutine(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
        _agents = pathfindingManager.Agents;
        _dirCalculator = new AgentDirectionCalculator(_agents);
    }

    public void Update(float deltaTime)
    {
        _dirCalculator.CalculateDirections();
    }
}
