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
    public NativeArray<float2> AgentPositionChangeBuffer;
    public void Execute(int index)
    {
        AgentMovementData agentData = AgentMovementDataArray[index];
        if((agentData.Status & AgentStatus.Moving) != AgentStatus.Moving) { return; }
        if (!AgentPositionChangeBuffer[index].Equals(0)) { return; }
        float2 agentPos = new float2(agentData.Position.x, agentData.Position.z);
        float2 totalResolution = 0;
        float agentRadius = agentData.Radius;
        float maxOverlapping = 0;

        for(int i = 0; i < HashGridArray.Length; i++)
        {
            GridTravesalData travData = SpatialGridUtils.GetGridTraversalData(agentPos, agentRadius, i);
            UnsafeList<HashTile> hashGrid = HashGridArray[i];

            for (int j = travData.botLeft; j <= travData.topLeft; j+=travData.gridColAmount)
            {
                for(int k = j; k < j + travData.horizontalSize; k++)
                {
                    HashTile tile = hashGrid[k];
                    int end = tile.Start + tile.Length;
                    for(int m = tile.Start; m < end; m++)
                    {
                        if(m == index) { continue; }

                        AgentStatus mateStatus = AgentMovementDataArray[m].Status;
                        float mateRadius = AgentMovementDataArray[m].Radius;
                        float3 matePosition = AgentMovementDataArray[m].Position;

                        if (mateStatus == 0) { continue; }

                        float2 matePos = new float2(matePosition.x, matePosition.z);
                        float desiredDistance = agentRadius + mateRadius;
                        desiredDistance *= math.select(1f, 0.85f, (mateStatus & AgentStatus.Moving) == AgentStatus.Moving);
                        float distance = math.distance(matePos, agentPos);
                        float overlapping = desiredDistance - distance;

                        if(overlapping <= 0) { continue; }


                        overlapping = math.select(overlapping / 2, overlapping, (mateStatus & AgentStatus.HoldGround) == AgentStatus.HoldGround);
                        float2 resolutionDirection = (agentPos - matePos) / distance;
                        totalResolution += math.select(resolutionDirection * overlapping, 0f, distance == 0);
                        maxOverlapping = math.select(overlapping, maxOverlapping, maxOverlapping >= overlapping);
                    }
                }
            }
        }
        float totalResolutionLen = math.length(totalResolution);
        totalResolution = math.select(totalResolution, totalResolution / totalResolutionLen * maxOverlapping, totalResolutionLen > maxOverlapping);
        AgentPositionChangeBuffer[index] = totalResolution;
    }
}