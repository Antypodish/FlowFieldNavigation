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

        for (int i = 0; i < agents.Length; i++)
        {
            if (agents[i].Avoidance == AvoidanceStatus.L)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawCube(agents[i].Position, new Vector3(0.2f, 0.2f, 0.2f));
            }
            else if (agents[i].Avoidance == AvoidanceStatus.R)
            {
                Gizmos.color = Color.black;
                Gizmos.DrawCube(agents[i].Position, new Vector3(0.2f, 0.2f, 0.2f));
            }
        }
    }
}