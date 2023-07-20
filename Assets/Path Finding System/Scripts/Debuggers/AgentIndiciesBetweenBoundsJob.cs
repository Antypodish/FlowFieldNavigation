using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

[BurstCompile]
public struct AgentIndiciesBetweenBoundsJob : IJob
{
    public float3 Bound1;
    public float3 Bound2;
    public NativeArray<float3> ScreenPositions;
    public NativeList<int> AgentIndiciesBetweenBounds;
    public void Execute()
    {
        float minY = math.min(Bound1.y, Bound2.y);
        float maxY = math.max(Bound1.y, Bound2.y);

        float minX = math.min(Bound1.x, Bound2.x);
        float maxX = math.max(Bound1.x, Bound2.x);

        for(int i = 0; i < ScreenPositions.Length; i++)
        {
            float3 pos = ScreenPositions[i];
            if(pos.x < minX) { continue; }
            else if(pos.y < minY) { continue; }
            else if(pos.y > maxY) { continue; }
            else if(pos.x > maxX) { continue; }
            AgentIndiciesBetweenBounds.Add(i);
        }
    }
}