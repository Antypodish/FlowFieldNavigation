using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;
using Unity.Mathematics;
using System;


namespace FlowFieldNavigation
{
    [BurstCompile]
    internal struct AgentPositionChangeResetJob : IJob
    {
        [WriteOnly] internal NativeArray<float3> PositionChanges;

        public void Execute()
        {
            for (int i = 0; i < PositionChanges.Length; i++)
            {
                PositionChanges[i] = float3.zero;
            }
        }
    }
}
