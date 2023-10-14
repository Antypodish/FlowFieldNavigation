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
    [ReadOnly] public UnsafeList<float> PathStopDistances;
    
    public void Execute(int index, TransformAccess transform)
    {
        AgentData data = AgentDataArray[index];
        if(data.Direction.x == 0 && data.Direction.y == 0){ return; }

        //STOP IF CLOSE ENOUGH
        float stopDistance = PathStopDistances[data.StopDistanceIndex];
        float3 pos = transform.position;
        if ((data.Status & AgentStatus.Moving) == AgentStatus.Moving && math.distance(pos, new float3(data.Destination.x, pos.y, data.Destination.y)) <= stopDistance)
        {
            data.Direction = 0;
            data.Status = ~(~data.Status | AgentStatus.Moving);
            AgentDataArray[index] = data;
        }
        float3 direction = new float3(data.Direction.x, 0f, data.Direction.y);
        float3 seperation = new float3(data.Seperation.x, 0f, data.Seperation.y);
        float3 resultingDirection = direction + seperation;
        if(math.length(direction + seperation) > 1) { resultingDirection = math.normalize(resultingDirection); }
        float3 newPos = pos + (resultingDirection) * data.Speed * DeltaTime;
        transform.position = newPos;
        data.Position = newPos;
        AgentDataArray[index] = data;
    }
}