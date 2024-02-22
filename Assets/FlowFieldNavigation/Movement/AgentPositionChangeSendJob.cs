using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Jobs;

namespace FlowFieldNavigation
{

    [BurstCompile]
    internal struct AgentPositionChangeSendJob : IJobParallelForTransform
    {
        [ReadOnly] internal NativeArray<bool> AgentUseNavigationMovementFlags;
        [ReadOnly] internal NativeArray<float3> AgentPositionChangeBuffer;
        [ReadOnly] internal NativeArray<int> NormalToHashed;
        public void Execute(int index, TransformAccess transform)
        {
            if (!AgentUseNavigationMovementFlags[index]) { return; }
            int hashedIndex = NormalToHashed[index];
            float3 change = AgentPositionChangeBuffer[hashedIndex];
            transform.position = transform.position + (Vector3)change;
        }
    }


}