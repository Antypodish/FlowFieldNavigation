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
        NativeArray<AgentData> agentData = _pathfindingManager.AgentDataContainer.AgentDataList.AsArray();
        for(int i = 0; i < agentData.Length; i++)
        {
            Vector3 pos = agentData[i].Position;
            pos.y = 2f;
            Vector3 agentDirection = agentData[i].Direction3;
            Gizmos.DrawLine(pos, pos + agentDirection);
        }
    }
}
