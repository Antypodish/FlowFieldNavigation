using System.Collections.Generic;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
namespace FlowFieldNavigation
{
    internal class AgentDataIndexManager
    {
        internal NativeList<AgentDataReferance> AgentDataReferances;
        internal NativeList<int> RemovedAgentDataReferances;
        internal AgentDataIndexManager()
        {
            AgentDataReferances = new NativeList<AgentDataReferance>(Allocator.Persistent);
            RemovedAgentDataReferances = new NativeList<int>(Allocator.Persistent);
        }

        internal int CreateAgentReferance()
        {
            if (RemovedAgentDataReferances.IsEmpty)
            {
                AgentDataReferances.Add(new AgentDataReferance());
                return AgentDataReferances.Length - 1;
            }
            else
            {
                int indexToCreate = RemovedAgentDataReferances[0];
                RemovedAgentDataReferances.RemoveAtSwapBack(0);
                return indexToCreate;
            }
        }
        internal int AgentReferanceToAgentDataIndex(AgentReferance agentReferance)
        {
            int referanceIndex = agentReferance.GetIndexNonchecked();
            int dataIndex = AgentDataReferances[referanceIndex].GetIndexNonchecked();
            return dataIndex;
        }
    }
}
