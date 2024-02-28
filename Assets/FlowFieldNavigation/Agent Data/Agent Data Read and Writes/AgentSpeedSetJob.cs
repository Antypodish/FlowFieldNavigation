using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;

namespace FlowFieldNavigation
{
    [BurstCompile]
    internal struct AgentSpeedSetJob : IJob
    {
        [ReadOnly] internal NativeArray<SetSpeedReq> SetSpeedRequests;
        internal NativeArray<AgentData> AgentDataArray;
        public void Execute()
        {
            for (int i = 0; i < SetSpeedRequests.Length; i++)
            {
                SetSpeedReq req = SetSpeedRequests[i];
                AgentData agent = AgentDataArray[req.AgentIndex];
                agent.Speed = req.NewSpeed;
                AgentDataArray[req.AgentIndex] = agent;
            }
        }
    }
}
