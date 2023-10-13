using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Jobs;

[BurstCompile]
public struct AgentPositionChangeSendJob : IJobParallelForTransform
{
    [ReadOnly] public NativeArray<float2> AgentPositionChangeBuffer;
    public void Execute(int index, TransformAccess transform)
    {
        float2 change = AgentPositionChangeBuffer[index];
        transform.position = transform.position + new Vector3(change.x, 0f, change.y);
    }
}
