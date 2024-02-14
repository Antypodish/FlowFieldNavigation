using Unity.Collections;
using UnityEngine;

internal class EditorAvoidanceDirectionDebugger
{
    FlowFieldNavigationManager _navigationManager;
    internal EditorAvoidanceDirectionDebugger(FlowFieldNavigationManager navigationManager)
    {
        _navigationManager = navigationManager;
    }

    internal void Debug()
    {
        NativeArray<AgentData> agents = _navigationManager.AgentDataContainer.AgentDataList.AsArray();

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