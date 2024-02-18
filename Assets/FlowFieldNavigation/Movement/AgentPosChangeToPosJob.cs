using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace FlowFieldNavigation
{
    [BurstCompile]
    internal struct AgentPosChangeToPosJob : IJob
    {
        internal NativeArray<float3> PositionChanges;
        internal NativeArray<AgentMovementData> AgentMovementDataArrayRaw;
        public void Execute()
        {
            for(int i = 0; i < AgentMovementDataArrayRaw.Length; i++)
            {
                AgentMovementData agentData = AgentMovementDataArrayRaw[i];
                agentData.Position += PositionChanges[i];
                AgentMovementDataArrayRaw[i] = agentData;
                PositionChanges[i] = float3.zero;
            }
        }
    }
}
