using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

internal class EditorAgentGroupDebugger
{
    PathfindingManager _pathfindingManager;
    Color[] _colors;
    public EditorAgentGroupDebugger(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;

        _colors = new Color[]{
            new Color(0,0,0),
            new Color(1,0,0),
            new Color(0,1,0),
            new Color(1,1,0),
            new Color(0,0,1),
            new Color(1,0,1),
            new Color(0,1,1),
            new Color(1,1,1),
        };
    }
    public void OnEnable()
    {
        Gizmos.color = Color.white;
        List<FlowFieldAgent> agents = _pathfindingManager.AgentDataContainer.Agents;
        for (int i = 0; i < agents.Count; i++)
        {
            FlowFieldAgent agent = agents[i];
            int colorIndex = i % _colors.Length;
            agent.GetComponentInChildren<MeshRenderer>().material.color = _colors[colorIndex];
        }
    }
}
