using System.Collections.Generic;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
namespace FlowFieldNavigation
{
    internal class AgentDataReferanceManager
    {
        internal NativeList<AgentDataReferance> AgentDataReferances;
        internal NativeList<int> AgentDataReferanceWriteIndicies;
        internal NativeList<AgentDataReferanceState> AgentDataRefStates;
        internal NativeList<int> RemovedAgentDataReferances;

        internal AgentDataReferanceManager()
        {
            AgentDataReferances = new NativeList<AgentDataReferance>(Allocator.Persistent);
            RemovedAgentDataReferances = new NativeList<int>(Allocator.Persistent);
            AgentDataRefStates = new NativeList<AgentDataReferanceState>(Allocator.Persistent);
            AgentDataReferanceWriteIndicies = new NativeList<int>(Allocator.Persistent);
        }

        internal int CreateAgentReferance()
        {
            if (RemovedAgentDataReferances.IsEmpty)
            {
                AgentDataReferances.Add(new AgentDataReferance());
                AgentDataRefStates.Add(AgentDataReferanceState.BeingAdded);
                AgentDataReferanceWriteIndicies.Add(-1);
                return AgentDataReferances.Length - 1;
            }
            else
            {
                int indexToCreate = RemovedAgentDataReferances[0];
                RemovedAgentDataReferances.RemoveAtSwapBack(0);
                AgentDataRefStates[indexToCreate] = AgentDataReferanceState.BeingAdded;
                AgentDataReferanceWriteIndicies[indexToCreate] = -1;
                return indexToCreate;
            }
        }
        internal int AgentDataReferanceIndexToAgentDataIndex(int agentDataReferanceIndex)
        {
            int dataIndex = AgentDataReferances[agentDataReferanceIndex].GetIndexNonchecked();
            return dataIndex;
        }
        internal bool TryAgentDataReferanceIndexToAgentDataIndex(int agentDataReferanceIndex, out int agentDataIndex)
        {
            if (AgentDataRefStates[agentDataReferanceIndex] == AgentDataReferanceState.BeingAdded)
            {
                agentDataIndex = -1;
                return false;
            }
            agentDataIndex = AgentDataReferances[agentDataReferanceIndex].GetIndexNonchecked();
            return true;
        }
        internal AgentDataReferance GetAgentDataRefWithState(int agentDataRefIndex, out AgentDataReferanceState state)
        {
            state = AgentDataRefStates[agentDataRefIndex];
            return AgentDataReferances[agentDataRefIndex];
        }
    }
    internal enum AgentDataReferanceState : byte
    {
        Removed = 0,
        Added,
        BeingAdded,
    }
}
