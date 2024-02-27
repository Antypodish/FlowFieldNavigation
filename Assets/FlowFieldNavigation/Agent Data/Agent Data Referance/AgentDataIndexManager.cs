using System.Collections.Generic;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
namespace FlowFieldNavigation
{
    internal class AgentDataIndexManager
    {
        internal NativeList<AgentDataReferance> AgentDataReferances;

        internal AgentDataIndexManager()
        {
            AgentDataReferances = new NativeList<AgentDataReferance>(Allocator.Persistent);
        }

        internal int CreateAgentReferance()
        {
            AgentDataReferances.Add(new AgentDataReferance());
            return AgentDataReferances.Length - 1;
        }
        internal int AgentReferanceToAgentDataIndex(AgentReferance agentReferance)
        {
            int referanceIndex = agentReferance.GetIndexNonchecked();
            //UnityEngine.Debug.Log("ref index: " +referanceIndex);
            int dataIndex = AgentDataReferances[referanceIndex].GetIndexNonchecked();
            //UnityEngine.Debug.Log("data index: " + dataIndex);
            return dataIndex;
        }
    }
}
