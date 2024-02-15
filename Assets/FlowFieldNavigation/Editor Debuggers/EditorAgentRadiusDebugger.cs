using Unity.Collections;
using UnityEngine;

namespace FlowFieldNavigation
{
    internal class EditorAgentRadiusDebugger
    {
        FlowFieldNavigationManager _navigationManager;
        internal EditorAgentRadiusDebugger(FlowFieldNavigationManager navigationManager)
        {
            _navigationManager = navigationManager;
        }
        internal void DebugSeperationRadius()
        {
            Gizmos.color = Color.white;
            NativeArray<AgentData> agents = _navigationManager.AgentDataContainer.AgentDataList.AsArray();
            for (int i = 0; i < agents.Length; i++)
            {
                Gizmos.DrawWireSphere(agents[i].Position, agents[i].Radius);
            }
        }
    }

}
