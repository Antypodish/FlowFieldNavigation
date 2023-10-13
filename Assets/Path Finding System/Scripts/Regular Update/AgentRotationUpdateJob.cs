using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Jobs;

[BurstCompile]
public struct AgentRotationUpdateJob : IJobParallelForTransform
{
    [ReadOnly] public NativeArray<AgentData> agentData;
    public void Execute(int index, TransformAccess transform)
    {
        float2 direction = agentData[index].Direction;
        float3 desiredForward = new float3(direction.x, 0f, direction.y);
        transform.rotation = quaternion.LookRotation(desiredForward, new float3(0, 1, 0));
    }
}
