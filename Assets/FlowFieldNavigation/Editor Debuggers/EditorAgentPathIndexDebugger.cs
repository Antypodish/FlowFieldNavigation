using UnityEditor;
using Unity.Collections;
using UnityEngine;
using Unity.Mathematics;
internal class EditorAgentPathIndexDebugger
{
    FlowFieldNavigationManager _navigationManager;

    public EditorAgentPathIndexDebugger(FlowFieldNavigationManager navigationManager)
    {
        _navigationManager = navigationManager;
    }

    public void Debug(FlowFieldAgent agent)
    {
        if(agent == null) { return; }
        if(agent.AgentDataIndex == -1) { return; }
        UnityEngine.Debug.Log(_navigationManager.AgentDataContainer.AgentCurPathIndicies[agent.AgentDataIndex]);
    }
}
