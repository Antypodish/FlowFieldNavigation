using Unity.Collections;
using UnityEngine;

internal class EditorAgentDirectionDebugger
{
    PathfindingManager _pathfindingManager;
    internal EditorAgentDirectionDebugger(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
    }

    internal void Debug()
    {
        Gizmos.color = Color.white;
        NativeArray<AgentData> agentData = _pathfindingManager.AgentDataContainer.AgentDataList;
        for(int i = 0; i < agentData.Length; i++)
        {
            Vector3 pos = agentData[i].Position;
            pos.y = 2f;
            Vector2 agentDirection = agentData[i].Direction;
            Gizmos.DrawLine(pos, pos + new Vector3(agentDirection.x, 0f, agentDirection.y));
        }
    }
}
