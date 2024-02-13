using Unity.Collections;
using UnityEngine;

internal class EditorAgentRadiusDebugger
{
    PathfindingManager _pathfindingManager;
    internal EditorAgentRadiusDebugger(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
    }
    internal void DebugSeperationRadius()
    {
        Gizmos.color = Color.white;
        NativeArray<AgentData> agents = _pathfindingManager.AgentDataContainer.AgentDataList.AsArray();
        for(int i = 0; i < agents.Length; i++)
        {
            Gizmos.DrawWireSphere(agents[i].Position, agents[i].Radius);
        }
    }
}