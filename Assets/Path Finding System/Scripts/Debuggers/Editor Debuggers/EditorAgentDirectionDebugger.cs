using Unity.Collections;
using UnityEngine;

public class EditorAgentDirectionDebugger
{
    PathfindingManager _pathfindingManager;
    public EditorAgentDirectionDebugger(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
    }

    public void Debug()
    {
        Gizmos.color = Color.white;
        NativeArray<AgentData> agentData = _pathfindingManager.AgentDataContainer.AgentDataList;
        NativeArray<Vector3> agentPositions = _pathfindingManager.AgentDataContainer.AgentPositions;
        for(int i = 0; i < agentData.Length; i++)
        {
            Vector3 pos = agentPositions[i];
            pos.y = 2f;
            Vector2 agentDirection = agentData[i].Velocity;
            Gizmos.DrawLine(pos, pos + new Vector3(agentDirection.x, 0f, agentDirection.y));
        }
    }
}
