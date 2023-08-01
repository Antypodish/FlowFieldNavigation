using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
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
        //DO NOT ADD IF UNWALKABLE
        UnsafeList<byte> costsG = _pathfindingingManager.FieldProducer.GetCostFieldWithOffset(0).CostsG;
        float tileSize = _pathfindingingManager.TileSize;
        int fieldColAmount = _pathfindingingManager.ColumnAmount;
        int2 general2d = new int2(Mathf.FloorToInt(position.x / tileSize), Mathf.FloorToInt(position.z / tileSize));
        int general1d = general2d.y * fieldColAmount + general2d.x;
        if (costsG[general1d] == byte.MaxValue) { return; }

        //AGENT ADDITION
        GameObject obj = GameObject.Instantiate(AgentPrefab);
        FlowFieldAgent flowFieldAgentComponent = obj.GetComponent<FlowFieldAgent>();
        obj.transform.position = new Vector3(position.x, flowFieldAgentComponent.GetLandOffset(), position.z);
    }
}