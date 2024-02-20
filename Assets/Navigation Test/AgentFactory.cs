using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using FlowFieldNavigation;
public class AgentFactory
{
    public GameObject AgentPrefab;
    FlowFieldNavigationManager _navigationManager;
    public AgentFactory(GameObject agentPrefab, FlowFieldNavigationManager navigationManager)
    {
        AgentPrefab = agentPrefab;
        _navigationManager = navigationManager;
    }

    public FlowFieldAgent AddAgent(Vector3 position)
    {
        //AGENT ADDITION
        GameObject obj = GameObject.Instantiate(AgentPrefab);
        FlowFieldAgent flowFieldAgentComponent = obj.GetComponent<FlowFieldAgent>();
        obj.transform.position = position;
        _navigationManager.Interface.RequestSubscription(flowFieldAgentComponent);
        return flowFieldAgentComponent;
    }
}