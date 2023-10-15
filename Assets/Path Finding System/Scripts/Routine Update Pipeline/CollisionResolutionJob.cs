using System.Diagnostics.CodeAnalysis;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine.Jobs;

[BurstCompile]
public struct CollisionResolutionJob : IJobParallelFor
{
    public AgentSpatialGridUtils SpatialGridUtils;
    [ReadOnly] public NativeArray<AgentMovementData> AgentMovementDataArray;
    [ReadOnly] public NativeArray<UnsafeList<HashTile>> HashGridArray;
    [WriteOnly] public NativeArray<float2> AgentPositionChangeBuffer;
    public void Execute(int index)
    {
        AgentMovementData agentData = AgentMovementDataArray[index];
        if((agentData.Status & AgentStatus.Moving) != AgentStatus.Moving) { return; }
        float2 agentPos = new float2(agentData.Position.x, agentData.Position.z);
        float2 totalResolution = 0;
        float agentRadius = agentData.Radius;
        float maxOverlapping = 0;

        for(int i = 0; i < HashGridArray.Length; i++)
        {
            GridTravesalData travData = SpatialGridUtils.GetGridTraversalData(agentPos, agentRadius, i);
            UnsafeList<HashTile> hashGrid = HashGridArray[i];

            for (int j = travData.botLeft; j <= travData.topLeft; j++)
            {
                for(int k = j; k < j + travData.horizontalSize; k++)
                {
                    HashTile tile = hashGrid[k];
                    for(int m = tile.Start; m < tile.Start + tile.Length; m++)
                    {
                        if(index == m) { continue; }
                        AgentMovementData mateData = AgentMovementDataArray[m];
                        if(mateData.Status == 0) { continue; }
                        float2 matePos = new float2(mateData.Position.x, mateData.Position.z);
                        float mateRadius = mateData.Radius;
                        float desiredDistance = agentRadius + mateRadius;
                        desiredDistance *= math.select(1f, 0.85f, (mateData.Status & AgentStatus.Moving) == AgentStatus.Moving);
                        float distance = math.distance(matePos, agentPos);
                        float overlapping = desiredDistance - distance;
                        if(overlapping < 0) { continue; }

                        overlapping = math.select(overlapping / 2, overlapping, (mateData.Status & AgentStatus.HoldGround) == AgentStatus.HoldGround);
                        float2 resolutionDirection = math.normalizesafe(agentPos - matePos);
                        totalResolution += resolutionDirection * overlapping;
                        maxOverlapping = math.select(overlapping, maxOverlapping, maxOverlapping > overlapping);
                    }
                }
            }
        }
        totalResolution = math.select(totalResolution, math.normalize(totalResolution) * maxOverlapping, math.length(totalResolution) > maxOverlapping);
        float3 curPos = AgentMovementDataArray[index].Position;
        AgentPositionChangeBuffer[index] = new float2(totalResolution.x, totalResolution.y); ;
    }
}