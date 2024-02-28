using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Jobs;
using System.Diagnostics;
namespace FlowFieldNavigation
{
    internal class AgentAdditionSystem
    {
        FlowFieldNavigationManager _navManager;
        NativeList<bool> _subReqAgentDataRefIndiciesValidityFlags;
        internal AgentAdditionSystem(FlowFieldNavigationManager navManager)
        {
            _navManager = navManager;
            _subReqAgentDataRefIndiciesValidityFlags = new NativeList<bool>(Allocator.Persistent);
        }
        public void AddAgents(NativeArray<int> subReqAgentDataRefIndicies, NativeArray<AgentInput> subReqAgentInputs, List<Transform> subReqAgentTransforms)
        {
            _subReqAgentDataRefIndiciesValidityFlags.Length = subReqAgentDataRefIndicies.Length;
            NativeArray<AgentDataReferance> agentDataRefs = _navManager.AgentReferanceManager.AgentDataReferances.AsArray();
            NativeArray<AgentDataReferanceState> agentDataRefStates = _navManager.AgentReferanceManager.AgentDataRefStates.AsArray();

            AgentDataContainer agentDataContainer = _navManager.AgentDataContainer;
            AgentAdditionJob agentAddition = new AgentAdditionJob()
            {
                MaxAgentRadius = FlowFieldUtilities.MaxAgentSize,
                SubReqAgentDataRefIndicies = subReqAgentDataRefIndicies,
                SubReqAgentInputs = subReqAgentInputs,
                AgentDataReferanceStates = agentDataRefStates,
                AgentDataReferances = agentDataRefs,
                AgentCurPathIndicies = agentDataContainer.AgentCurPathIndicies,
                AgentDataList = agentDataContainer.AgentDataList,
                AgentDataRefIndiciesPerAgent = agentDataContainer.AgentReferanceIndicies,
                AgentDestinationReachedArray = agentDataContainer.AgentDestinationReachedArray,
                AgentFlockIndicies = agentDataContainer.AgentFlockIndicies,
                AgentNewPathIndicies = agentDataContainer.AgentNewPathIndicies,
                AgentRadii = agentDataContainer.AgentRadii,
                AgentUseNavigationMovementFlags = agentDataContainer.AgentUseNavigationMovementFlags,
                SubReqAgentDataRefIndiciesValidityFlags = _subReqAgentDataRefIndiciesValidityFlags.AsArray(),
            };
            agentAddition.Schedule().Complete();

            TransformAccessArray agentTransforms = agentDataContainer.AgentTransforms;
            for(int i = 0; i < _subReqAgentDataRefIndiciesValidityFlags.Length; i++)
            {
                if (_subReqAgentDataRefIndiciesValidityFlags[i])
                {
                    agentTransforms.Add(subReqAgentTransforms[i]);
                }
            }
        }
    }
}
