using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace FlowFieldNavigation
{
    internal class AgentAdditionSystem
    {
        FlowFieldNavigationManager _navManager;
        internal AgentAdditionSystem(FlowFieldNavigationManager navManager)
        {
            _navManager = navManager;
        }
        public void AddAgents(NativeArray<int> subReqAgentDataRefIndicies, NativeArray<AgentInput> subReqAgentInputs, List<Transform> subReqAgentTransforms)
        {
            NativeArray<AgentDataReferance> agentDataRefs = _navManager.AgentReferanceManager.AgentDataReferances.AsArray();
            NativeArray<AgentDataReferanceState> agentDataRefStates = _navManager.AgentReferanceManager.AgentDataRefStates.AsArray();
            for (int i = 0; i < subReqAgentDataRefIndicies.Length; i++)
            {
                int agentDataRefIndex = subReqAgentDataRefIndicies[i];
                AgentDataReferanceState agentDataRefState = agentDataRefStates[agentDataRefIndex];
                if(agentDataRefState != AgentDataReferanceState.BeingAdded) { continue; }
                agentDataRefStates[agentDataRefIndex] = AgentDataReferanceState.Added;
                int agentDataIndex = _navManager.AgentDataContainer.Subscribe(subReqAgentInputs[i], subReqAgentTransforms[i]);
                agentDataRefs[agentDataRefIndex] = new AgentDataReferance(agentDataIndex);
                _navManager.AgentDataContainer.AgentReferanceIndicies.Add(agentDataRefIndex);
            }
        }
    }
}
