using Unity.Collections;
using UnityEngine;
using UnityEngine.Jobs;

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
            TransformAccessArray transforms = _navigationManager.AgentDataContainer.AgentTransforms;
            for (int i = 0; i < agents.Length; i++)
            {
                Gizmos.DrawWireSphere(transforms[i].position, agents[i].Radius);
            }
        }
    }

}
