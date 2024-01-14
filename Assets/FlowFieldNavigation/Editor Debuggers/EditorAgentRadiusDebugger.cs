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
        for(int i = 0; i < agents.Length; i++)
        {
            Gizmos.DrawWireSphere(agents[i].Position, agents[i].Radius);
        }
    }
}