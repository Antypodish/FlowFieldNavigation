using System.Collections.Generic;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
namespace FlowFieldNavigation
{
    internal class AgentDataIndexManager
    {
        internal NativeList<AgentIndexReferance> AgentDataReferances;

        internal AgentDataIndexManager()
        {
            AgentDataReferances = new NativeList<AgentIndexReferance>(Allocator.Persistent);
        }

        internal int CreateAgentReferance()
        {
            AgentDataReferances.Add(new AgentIndexReferance());
            return AgentDataReferances.Length - 1;
        }
        internal int AgentReferanceToAgentDataIndex(AgentIndexReferance agentReferance)
        {
            int referanceIndex = agentReferance.GetIndexNonchecked();
            //UnityEngine.Debug.Log("ref index: " +referanceIndex);
            int dataIndex = AgentDataReferances[referanceIndex].GetIndexNonchecked();
            //UnityEngine.Debug.Log("data index: " + dataIndex);
            return dataIndex;
        }
    }
}
