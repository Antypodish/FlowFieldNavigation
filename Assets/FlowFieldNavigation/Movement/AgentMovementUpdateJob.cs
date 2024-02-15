using Unity.Collections;
using UnityEngine;
using UnityEngine.Jobs;
using Unity.Mathematics;
using System.Runtime.InteropServices.WindowsRuntime;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;

namespace FlowFieldNavigation
{

    [BurstCompile]
    internal struct AgentMovementUpdateJob : IJobParallelForTransform
    {
        internal float DeltaTime;
        [ReadOnly] internal NativeArray<AgentData> AgentDataArray;

        public void Execute(int index, TransformAccess transform)
        {
            AgentData data = AgentDataArray[index];
            float speed = data.Speed;
            if ((data.Status & AgentStatus.Moving) != AgentStatus.Moving) { speed = 0f; }
            float3 pos = transform.position;
            float3 direction = data.DirectionWithHeigth;
            float3 seperation = new float3(data.Seperation.x, 0f, data.Seperation.y);
            float3 resultingDirection = direction;
            float3 newPos = pos + (resultingDirection) * speed * DeltaTime + seperation * DeltaTime;
            transform.position = newPos;
        }
    }

}