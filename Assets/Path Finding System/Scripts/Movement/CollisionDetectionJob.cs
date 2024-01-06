using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

[BurstCompile]
public struct CollisionDetectionJob : IJob
{
    public int AgentStartIndex;
    public int AgentCount;
    [ReadOnly] public AgentSpatialHashGrid AgentSpatialHashGrid;
    [ReadOnly] public NativeSlice<int> AgentMaxCollisionCounts;
    [NativeDisableContainerSafetyRestriction] public NativeSlice<CollisionSlice> AgentCollisionSlices;
    [NativeDisableContainerSafetyRestriction] public NativeSlice<Collision> Collisions;
    public void Execute()
    {
        for(int agentSlicedIndex = AgentStartIndex; agentSlicedIndex < AgentStartIndex + AgentCount; agentSlicedIndex++)
        {
            int agentHashedIndex = AgentStartIndex + agentSlicedIndex;
            CollisionSlice agentCollisionSlice = AgentCollisionSlices[agentSlicedIndex];
            int agentMaxCollisionCount = AgentMaxCollisionCounts[agentSlicedIndex];
            //DETECT COLLISIONS
            //PUSH THEM TO THE SLICE
        }
    }
}
public struct Collision
{
    public float2 MatePos;
    public float MateRadius;
    public float ResolutionMultiplier;
}
public struct CollisionSlice
{
    public int CollisionStart;
    public int CollisionCount;
}