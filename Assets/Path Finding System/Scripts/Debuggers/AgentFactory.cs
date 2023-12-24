using Unity.Collections;
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
        NativeArray<byte> costsG = _pathfindingingManager.FieldProducer.GetCostFieldWithOffset(0).Costs;
        float tileSize = _pathfindingingManager.TileSize;

        //int2 general2d = new int2(Mathf.FloorToInt(position.x / tileSize), Mathf.FloorToInt(position.z / tileSize));
        //LocalIndex1d local = FlowFieldUtilities.GetLocal1D(general2d, FlowFieldUtilities.SectorColAmount, FlowFieldUtilities.SectorMatrixColAmount);
        //if (costsG[local.sector * FlowFieldUtilities.SectorTileAmount + local.index] == byte.MaxValue) { return; }

        //AGENT ADDITION
        GameObject obj = GameObject.Instantiate(AgentPrefab);
        FlowFieldAgent flowFieldAgentComponent = obj.GetComponent<FlowFieldAgent>();
        obj.transform.position = new Vector3(position.x, flowFieldAgentComponent.GetLandOffset(), position.z);
    }
}