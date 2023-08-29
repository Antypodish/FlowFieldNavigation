using Unity.Collections;
using UnityEngine;

internal class EditorAvoidanceDirectionDebugger
{
    PathfindingManager _pathfindingManager;
    public EditorAvoidanceDirectionDebugger(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
    }

    public void Debug()
    {
        NativeArray<AgentData> agents = _pathfindingManager.AgentDataContainer.AgentDataList;
        NativeArray<Vector3> positions = _pathfindingManager.AgentDataContainer.AgentPositions;

        for (int i = 0; i < agents.Length; i++)
        {
            if (agents[i].Avoidance == AvoidanceStatus.CW)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawCube(positions[i], new Vector3(0.2f, 0.2f, 0.2f));
            }
            else if (agents[i].Avoidance == AvoidanceStatus.CCW)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawCube(positions[i], new Vector3(0.2f, 0.2f, 0.2f));
            }
        }
    }
}