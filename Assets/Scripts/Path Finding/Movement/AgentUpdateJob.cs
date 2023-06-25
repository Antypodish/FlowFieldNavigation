using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
public struct AgentUpdateJob : IJobParallelFor
{
    public float DeltaTime;
    [ReadOnly] public NativeList<AgentData> AgentDatas;
    public UnsafeList<Vector3> AgentPositions;
    public void Execute(int index)
    {
        AgentData data = AgentDatas[index];
        float3 agentPosition = (float3)AgentPositions[index];
        float2 direction = (float2) data.Direction;
        float2 destination = (float2) data.Destination;
        if (direction.x == 0 && direction.y == 0)
        {
            float3 newDestnation = new Vector3(destination.x, agentPosition.y, destination.y);
            agentPosition = Vector3.MoveTowards(agentPosition, newDestnation, data.Speed * DeltaTime);
        }
        else
        {
            float3 newDirection = new Vector3(direction.x, 0f, direction.y);
            agentPosition += newDirection * data.Speed * DeltaTime;
        }
        AgentPositions[index] = (Vector3) agentPosition;
    }
}