using Unity.Collections;
using UnityEngine;
using UnityEngine.Jobs;
using Unity.Mathematics;
using System.Runtime.InteropServices.WindowsRuntime;
using Unity.Burst;

[BurstCompile]
public struct AgentMovementUpdateJob : IJobParallelForTransform
{
    public float DeltaTime;
    public NativeArray<AgentData> AgentDataArray;
    public void Execute(int index, TransformAccess transform)
    {
        AgentData data = AgentDataArray[index];
        if(data.Direction.x == 0 && data.Direction.y == 0){ return; }

        if (data.Direction.x == -1f && data.Direction.y == -1f)
        {
            float3 destination = new float3(data.Destination.x, transform.position.y, data.Destination.y);
            Vector3 pos = Vector3.MoveTowards(transform.position, destination, data.Speed * DeltaTime);
            transform.position = pos;
        }
        else
        {
            float3 direction = new float3(data.Direction.x, 0f, data.Direction.y);
            float3 pos = transform.position;
            transform.position = pos + (direction * data.Speed * DeltaTime);
        }
    }
}