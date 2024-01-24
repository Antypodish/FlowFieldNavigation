using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public class AgentFactory
{
    public GameObject AgentPrefab;
    PathfindingManager _pathfindingManager;
    public AgentFactory(GameObject agentPrefab, PathfindingManager pathfindingManager)
    {
        AgentPrefab = agentPrefab;
        _pathfindingManager = pathfindingManager;
    }

    public void AddAgent(Vector3 position)
    {
        //AGENT ADDITION
        GameObject obj = GameObject.Instantiate(AgentPrefab);
        FlowFieldAgent flowFieldAgentComponent = obj.GetComponent<FlowFieldAgent>();
        obj.transform.position = position;
        _pathfindingManager.Interface.RequestSubscription(flowFieldAgentComponent);
    }
}