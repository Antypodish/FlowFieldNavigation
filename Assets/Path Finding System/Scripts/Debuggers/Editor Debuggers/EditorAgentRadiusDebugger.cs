using Assets.Path_Finding_System.Scripts;
using Unity.Collections;
using UnityEngine;

public class EditorAgentRadiusDebugger
{
    PathfindingManager _pathfindingManager;
    public EditorAgentRadiusDebugger(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
    }
    public void DebugSeperationRadius()
    {
        Gizmos.color = Color.white;
        NativeArray<AgentData> agents = _pathfindingManager.AgentDataContainer.AgentDataList;
        NativeArray<Vector3> agentPositions = _pathfindingManager.AgentDataContainer.AgentPositions;
        for(int i = 0; i < agents.Length; i++)
        {
            Gizmos.DrawWireSphere(agentPositions[i], agents[i].Radius);
        }
    }
}