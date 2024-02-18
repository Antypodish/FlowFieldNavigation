using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;

namespace FlowFieldNavigation
{
    [BurstCompile]
    internal struct CollisionFinalPositionChangeJob : IJob
    {
        [ReadOnly] internal NativeArray<float3> AgentInitialPositionsNormal;
        [ReadOnly] internal NativeArray<AgentMovementData> AgentMovementDataArrayHashed;
        [ReadOnly] internal NativeArray<int> NormalToHashed;
        [WriteOnly] internal NativeArray<float3> FinalPositionChanges;
        public void Execute()
        {
            for(int i = 0; i < AgentInitialPositionsNormal.Length; i++)
            {
                float3 initialPosition = AgentInitialPositionsNormal[i];
                int hashedIndex = NormalToHashed[i];
                float3 finalPosition = AgentMovementDataArrayHashed[hashedIndex].Position;
                FinalPositionChanges[hashedIndex] = finalPosition - initialPosition;
            }
        }
    }
}
