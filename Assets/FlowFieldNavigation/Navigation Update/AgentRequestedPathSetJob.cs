using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;

namespace FlowFieldNavigation
{
    [BurstCompile]
    internal struct AgentRequestedPathSetJob : IJob
    {
        internal int RequestedPathIndex;

        [ReadOnly] internal NativeArray<AgentReferance> SourceAgentReferances;
        [ReadOnly] internal NativeArray<AgentDataReferance> AgentDataReferances;
        [WriteOnly] internal NativeArray<int> AgentRequestedPathIndicies;
        public void Execute()
        {
            for (int i = 0; i < SourceAgentReferances.Length; i++)
            {
                AgentReferance sourceAgentReferance = SourceAgentReferances[i];
                AgentDataReferance agentDataReferance = AgentDataReferances[sourceAgentReferance.GetIndexNonchecked()];
                int agentDataIndex = agentDataReferance.GetIndexNonchecked();
                AgentRequestedPathIndicies[agentDataIndex] = RequestedPathIndex;
            }
        }
    }
}
