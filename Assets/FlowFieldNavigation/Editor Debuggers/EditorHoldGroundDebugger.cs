using UnityEngine;
using Unity.Collections;
using System.Collections.Generic;

namespace FlowFieldNavigation
{
    internal class EditorHoldGroundDebugger
    {
        FlowFieldNavigationManager _navigationManager;
        internal EditorHoldGroundDebugger(FlowFieldNavigationManager navigationManager)
        {
            _navigationManager = navigationManager;
        }

        internal void Debug()
        {
            Gizmos.color = Color.yellow;

            NativeArray<AgentData> agents = _navigationManager.AgentDataContainer.AgentDataList.AsArray();

            for (int i = 0; i < agents.Length; i++)
            {
                if ((agents[i].Status & AgentStatus.HoldGround) != AgentStatus.HoldGround) { continue; }
                Gizmos.DrawCube(agents[i].Position, new Vector3(0.2f, 0.2f, 0.2f));
            }
        }
    }

}

