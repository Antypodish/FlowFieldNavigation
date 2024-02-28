using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;

namespace FlowFieldNavigation
{
    [BurstCompile]
    internal struct AgentAdditionJob :IJob
    {
        internal float MaxAgentRadius;

        [ReadOnly] internal NativeArray<int> SubReqAgentDataRefIndicies;
        [ReadOnly] internal NativeArray<AgentInput> SubReqAgentInputs;
        internal NativeArray<AgentDataReferance> AgentDataReferances;
        internal NativeArray<AgentDataReferanceState> AgentDataReferanceStates;
        internal NativeList<int> AgentDataRefIndiciesPerAgent;
        internal NativeList<AgentData> AgentDataList;
        internal NativeList<float> AgentRadii;
        internal NativeList<bool> AgentUseNavigationMovementFlags;
        internal NativeList<bool> AgentDestinationReachedArray;
        internal NativeList<int> AgentFlockIndicies;
        internal NativeList<int> AgentNewPathIndicies;
        internal NativeList<int> AgentCurPathIndicies;

        internal NativeArray<bool> SubReqAgentDataRefIndiciesValidityFlags;
        public void Execute()
        {
            for(int i = 0; i < SubReqAgentDataRefIndicies.Length; i++)
            {
                int agentDataRefIndex = SubReqAgentDataRefIndicies[i];
                AgentDataReferanceState agentDataRefState = AgentDataReferanceStates[agentDataRefIndex];
                if (agentDataRefState != AgentDataReferanceState.BeingAdded)
                {
                    SubReqAgentDataRefIndiciesValidityFlags[i] = false;
                    continue;
                }
                AgentDataReferanceStates[agentDataRefIndex] = AgentDataReferanceState.Added;
                int agentDataIndex = AgentDataList.Length;
                AgentInput agentInput = SubReqAgentInputs[i];
                AgentData data = new AgentData()
                {
                    Speed = agentInput.Speed,
                    Status = 0,
                    Destination = float2.zero,
                    Direction = float2.zero,
                    LandOffset = agentInput.LandOffset,
                };
                AgentRadii.Add(math.min(agentInput.Radius, MaxAgentRadius));
                AgentDataList.Add(data);
                AgentNewPathIndicies.Add(-1);
                AgentCurPathIndicies.Add(-1);
                AgentFlockIndicies.Add(0);
                AgentDestinationReachedArray.Add(false);
                AgentUseNavigationMovementFlags.Add(true);
                AgentDataReferances[agentDataRefIndex] = new AgentDataReferance(agentDataIndex);
                AgentDataRefIndiciesPerAgent.Add(agentDataRefIndex);

                //Set flag if sub req is really valid
                SubReqAgentDataRefIndiciesValidityFlags[i] = true;
            }
        }
    }
}
