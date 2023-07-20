using UnityEngine;

public class AgentFactory
{
    public GameObject AgentPrefab;
    PathfindingManager _pathfindingingManager;

    public AgentFactory(GameObject agentPrefab, PathfindingManager pathfindingingManager)
    {
        AgentPrefab = agentPrefab;
        _pathfindingingManager = pathfindingingManager;
    }

    public void AddAgent(Vector3 position)
    {
        GameObject obj = GameObject.Instantiate(AgentPrefab);
        FlowFieldAgent flowFieldAgentComponent = obj.GetComponent<FlowFieldAgent>();
        obj.transform.position = new Vector3(position.x, flowFieldAgentComponent.GetLandOffset(), position.z);
    }
}
