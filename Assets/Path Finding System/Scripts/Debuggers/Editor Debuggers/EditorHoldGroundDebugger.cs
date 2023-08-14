using UnityEngine;
using Unity.Collections;
using System.Collections.Generic;

public class EditorHoldGroundDebugger
{
    PathfindingManager _pathfindingManager;
    public EditorHoldGroundDebugger(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
    }

    public void Debug()
    {
        Gizmos.color = Color.yellow;

        NativeArray<AgentData> agents = _pathfindingManager.AgentDataContainer.AgentDataList;
        NativeArray<Vector3> positions = _pathfindingManager.AgentDataContainer.AgentPositions;

        for(int i = 0; i< agents.Length; i++)
        {            
            if ((agents[i].Status & AgentStatus.HoldGround) != AgentStatus.HoldGround) { continue; }
            Gizmos.DrawCube(positions[i], new Vector3(0.2f, 0.2f, 0.2f));
        }
    }
}
