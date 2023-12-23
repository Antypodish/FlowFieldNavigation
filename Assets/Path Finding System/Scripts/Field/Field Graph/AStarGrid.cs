using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

public struct AStarGrid
{
    public NativeArray<AStarTile> _integratedCosts;
    public NativeQueue<int> _searchQueue;

    public AStarGrid(int rowAmount, int colAmount)
    {
        _integratedCosts = new NativeArray<AStarTile>(rowAmount * colAmount, Allocator.Persistent);
        _searchQueue = new NativeQueue<int>(Allocator.Persistent);
    }
}
public struct AStarTile
{
    public bool Enqueued;
    public float IntegratedCost;

    public AStarTile(float integratedCost, bool enqueued)
    {
        Enqueued = enqueued;
        IntegratedCost = integratedCost;
    }
}