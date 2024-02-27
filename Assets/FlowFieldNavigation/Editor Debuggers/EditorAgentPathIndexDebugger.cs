using UnityEditor;
using Unity.Collections;
using UnityEngine;
using Unity.Mathematics;

namespace FlowFieldNavigation
{
    internal class EditorAgentPathIndexDebugger
    {
        FlowFieldNavigationManager _navigationManager;

        public EditorAgentPathIndexDebugger(FlowFieldNavigationManager navigationManager)
        {
            _navigationManager = navigationManager;
        }

        public void Debug(FlowFieldAgent agent)
        {
            if (agent == null) { return; }
            AgentReferance agentReferance = agent.AgentReferance;
            if (!agentReferance.IsValid()) { return; }
            int agentDataIndex = _navigationManager.AgentReferanceManager.AgentReferanceToAgentDataIndex(agentReferance);
            UnityEngine.Debug.Log(_navigationManager.AgentDataContainer.AgentCurPathIndicies[agentDataIndex]);
        }
    }

}
