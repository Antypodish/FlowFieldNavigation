using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public class AgentFactory
{
    public GameObject AgentPrefab;

    public AgentFactory(GameObject agentPrefab)
    {
        AgentPrefab = agentPrefab;
    }

    public void AddAgent(Vector3 position)
    {
        //AGENT ADDITION
        GameObject obj = GameObject.Instantiate(AgentPrefab);
        FlowFieldAgent flowFieldAgentComponent = obj.GetComponent<FlowFieldAgent>();
        obj.transform.position = new Vector3(position.x, flowFieldAgentComponent.GetLandOffset(), position.z);
    }
}