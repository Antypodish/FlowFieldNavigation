using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;


namespace FlowFieldNavigation
{
    [BurstCompile]
    internal struct AgentReferanceIndexToAgentDataIndexJob : IJob
    {
        [ReadOnly] internal NativeArray<int> AgentReferanceIndicies;
        [ReadOnly] internal NativeArray<AgentDataReferance> AgentReferances;
        [WriteOnly] internal NativeList<int> AgentDataIndicies;
        public void Execute()
        {
            for(int i = 0; i < AgentReferanceIndicies.Length; i++)
            {
                int agentReferanceIndex = AgentReferanceIndicies[i];
                AgentDataReferance referance = AgentReferances[agentReferanceIndex];
                AgentDataIndicies.Add(referance.GetIndexNonchecked());
            }
        }
    }
}
