using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Collections;

namespace FlowFieldNavigation
{
    [BurstCompile]
    internal struct AgentDataWriteTransferJob : IJob
    {
        [ReadOnly] internal NativeArray<AgentDataWrite> AgentDataWrites;
        [ReadOnly] internal NativeArray<AgentDataReferance> AgentDataReferances;
        [ReadOnly] internal NativeArray<AgentDataReferanceState> AgentDataReferanceStates;
        internal NativeArray<int> AgentDataReferanceWriteIndicies;
        internal NativeArray<int> AgentNewPathIndicies;
        public void Execute()
        {
            for(int i = 0; i < AgentDataWrites.Length; i++)
            {
                AgentDataWrite dataWrite = AgentDataWrites[i];
                AgentDataWriteOutput dataWriteOutput = dataWrite.GetOutput();
                AgentDataReferanceWriteIndicies[dataWriteOutput.AgentDataReferanceIndex] = -1;
                if (AgentDataReferanceStates[dataWriteOutput.AgentDataReferanceIndex] == AgentDataReferanceState.Removed) { continue; }
                AgentDataReferance dataRef = AgentDataReferances[dataWriteOutput.AgentDataReferanceIndex];
                int dataIndex = dataRef.GetIndexNonchecked();
                if((dataWriteOutput.WriteFlags & AgentDataWriteFlags.ReqPathIndexWritten) == AgentDataWriteFlags.ReqPathIndexWritten)
                {
                    AgentNewPathIndicies[dataIndex] = dataWriteOutput.ReqPathIndex;
                }
            }
        }
    }
}
