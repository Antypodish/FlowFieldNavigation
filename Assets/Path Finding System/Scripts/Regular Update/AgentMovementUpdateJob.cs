using Unity.Collections;
using UnityEngine;
using UnityEngine.Jobs;
using Unity.Mathematics;
using System.Runtime.InteropServices.WindowsRuntime;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;

[BurstCompile]
public struct AgentMovementUpdateJob : IJobParallelForTransform
{
    public float DeltaTime;
    public NativeArray<AgentData> AgentDataArray;
    [WriteOnly] public NativeArray<Vector3> AgentPositions;
    [ReadOnly] public UnsafeList<float> PathStopDistances;
    
    public void Execute(int index, TransformAccess transform)
    {
        AgentData data = AgentDataArray[index];
        if(data.Velocity.x == 0 && data.Velocity.y == 0){ return; }

        //STOP IF CLOSE ENOUGH
        float stopDistance = PathStopDistances[data.StopDistanceIndex];
        float3 pos = transform.position;
        if ((data.Status & AgentStatus.Moving) == AgentStatus.Moving && math.distance(pos, new float3(data.Destination.x, pos.y, data.Destination.y)) <= stopDistance)
        {
            data.Velocity = 0;
            data.Status = ~(~data.Status | AgentStatus.Moving);
            AgentDataArray[index] = data;
        }
        float3 direction = new float3(data.Velocity.x, 0f, data.Velocity.y);
        float3 newPos = pos + (direction * DeltaTime);
        transform.position = newPos;
        AgentPositions[index] = newPos;
    }
}