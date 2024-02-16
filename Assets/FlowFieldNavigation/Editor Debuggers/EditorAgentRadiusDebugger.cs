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
            NativeArray<float> agentRadii = _navigationManager.AgentDataContainer.AgentRadii.AsArray();
            TransformAccessArray transforms = _navigationManager.AgentDataContainer.AgentTransforms;
            for (int i = 0; i < agentRadii.Length; i++)
            {
                Gizmos.DrawWireSphere(transforms[i].position, agentRadii[i]);
            }
        }
    }

}
