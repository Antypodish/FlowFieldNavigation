using System.Collections.Generic;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
namespace FlowFieldNavigation
{
    internal class AgentDataIndexManager
    {
        internal NativeList<AgentDataReferance> AgentDataReferances;
        internal NativeList<AgentDataReferanceState> AgentDataRefStates;
        internal NativeList<int> RemovedAgentDataReferances;
        internal AgentDataIndexManager()
        {
            AgentDataReferances = new NativeList<AgentDataReferance>(Allocator.Persistent);
            RemovedAgentDataReferances = new NativeList<int>(Allocator.Persistent);
            AgentDataRefStates = new NativeList<AgentDataReferanceState>(Allocator.Persistent);
        }

        internal int CreateAgentReferance()
        {
            if (RemovedAgentDataReferances.IsEmpty)
            {
                AgentDataReferances.Add(new AgentDataReferance());
                AgentDataRefStates.Add(AgentDataReferanceState.BeingAdded);
                return AgentDataReferances.Length - 1;
            }
            else
            {
                int indexToCreate = RemovedAgentDataReferances[0];
                RemovedAgentDataReferances.RemoveAtSwapBack(0);
                AgentDataRefStates[indexToCreate] = AgentDataReferanceState.BeingAdded;
                return indexToCreate;
            }
        }
        internal int AgentDataReferanceIndexToAgentDataIndex(int agentDataReferanceIndex)
        {
            int dataIndex = AgentDataReferances[agentDataReferanceIndex].GetIndexNonchecked();
            return dataIndex;
        }
    }
    internal enum AgentDataReferanceState : byte
    {
        Removed = 0,
        Added,
        BeingAdded,
    }
}
