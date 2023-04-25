using UnityEngine;
using Unity.Collections;
using UnityEditor;

public class CostFieldDebugger
{
    PathfindingManager _pathfindingManager;
    
    public CostFieldDebugger(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
    }

    public void DebugCostField(int offset)
    {
        NativeArray<Vector3> tilePositions = _pathfindingManager.TilePositions;
        NativeArray<byte> costs = _pathfindingManager.CostFieldProducer.GetCostFieldWithOffset(offset).Costs;

        for(int i = 0; i < costs.Length; i++)
        {
            Vector3 pos = tilePositions[i];
            byte cost = costs[i];
            Handles.Label(pos, cost.ToString());
        }
    }

    
}
