using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Collections;
using System.Collections.Generic;
namespace FlowFieldNavigation
{
    internal class AgentAdditionSystem
    {
        FlowFieldNavigationManager _navManager;
        internal AgentAdditionSystem(FlowFieldNavigationManager navManager)
        {
            _navManager = navManager;
        }
        public void AddAgents(List<FlowFieldAgent> agentsToAdd, NativeList<int> subReqAgentDataRefIndicies)
        {
            NativeArray<AgentDataReferance> agentDataRefs = _navManager.AgentReferanceManager.AgentDataReferances.AsArray();
            NativeArray<AgentDataReferanceState> agentDataRefStates = _navManager.AgentReferanceManager.AgentDataRefStates.AsArray();
            for (int i = 0; i < agentsToAdd.Count; i++)
            {
                FlowFieldAgent agent = agentsToAdd[i];
                int agentDataRefIndex = subReqAgentDataRefIndicies[i];
                AgentDataReferanceState agentDataRefState = agentDataRefStates[agentDataRefIndex];
                if(agentDataRefState != AgentDataReferanceState.BeingAdded) { continue; }
                agentDataRefStates[agentDataRefIndex] = AgentDataReferanceState.Added;
                int agentDataIndex = _navManager.AgentDataContainer.Subscribe(agent);
                agentDataRefs[agentDataRefIndex] = new AgentDataReferance(agentDataIndex);
                _navManager.AgentDataContainer.AgentReferanceIndicies.Add(agentDataRefIndex);
            }
        }
    }
}
