using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Jobs;

[BurstCompile]
public struct AgentPositionChangeSendJob : IJobParallelForTransform
{
    [ReadOnly] public NativeArray<float3> AgentPositionChangeBuffer;
    [ReadOnly] public NativeArray<int> NormalToHashed;
    public void Execute(int index, TransformAccess transform)
    {
        int hashedIndex = NormalToHashed[index];
        float3 change = AgentPositionChangeBuffer[hashedIndex];
        transform.position = transform.position + (Vector3) change;
    }
}
