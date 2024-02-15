using Unity.Collections;
using UnityEngine;

namespace FlowFieldNavigation
{
    internal class EditorAgentDirectionDebugger
    {
        FlowFieldNavigationManager _navigationManager;
        internal EditorAgentDirectionDebugger(FlowFieldNavigationManager navigationManager)
        {
            _navigationManager = navigationManager;
        }

        internal void Debug()
        {
            Gizmos.color = Color.white;
            NativeArray<AgentData> agentData = _navigationManager.AgentDataContainer.AgentDataList.AsArray();
            for (int i = 0; i < agentData.Length; i++)
            {
                Vector3 pos = agentData[i].Position;
                pos.y = 2f;
                Vector3 agentDirection = agentData[i].DirectionWithHeigth;
                Gizmos.DrawLine(pos, pos + agentDirection);
            }
        }
    }



}