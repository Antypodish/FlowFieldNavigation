using UnityEditor;
using Unity.Collections;
using UnityEngine;
using Unity.Mathematics;
internal class EditorAgentPathIndexDebugger
{
    PathfindingManager _pathfindingManager;

    public EditorAgentPathIndexDebugger(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
    }

    public void Debug(FlowFieldAgent agent)
    {
        if(agent == null) { return; }
        if(agent.AgentDataIndex == -1) { return; }
        UnityEngine.Debug.Log(_pathfindingManager.AgentDataContainer.AgentCurPathIndicies[agent.AgentDataIndex]);
    }
}
