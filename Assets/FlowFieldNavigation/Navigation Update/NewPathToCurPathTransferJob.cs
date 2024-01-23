using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;

[BurstCompile]
internal struct NewPathToCurPathTransferJob : IJob
{
    internal NativeArray<AgentData> AgentDataArray;
    internal NativeArray<int> AgentCurPathIndicies;
    internal NativeArray<int> AgentNewPathIndicies;
    internal NativeArray<int> PathSubscribers;
    internal NativeArray<bool> AgentDestinationReachedArray;
    [ReadOnly] internal NativeArray<FinalPathRequest> FinalPathRequests;
    public void Execute()
    {
        for (int i = 0; i < AgentCurPathIndicies.Length; i++)
        {
            int curPathIndex = AgentCurPathIndicies[i];
            int newPathIndex = AgentNewPathIndicies[i];
            if (newPathIndex == -1) { continue; }
            FinalPathRequest finalReq = FinalPathRequests[newPathIndex];
            newPathIndex = finalReq.PathIndex;
            if (curPathIndex == -1)
            {
                AgentCurPathIndicies[i] = newPathIndex;
                AgentNewPathIndicies[i] = -1;
            }
            else
            {
                int subscriber = PathSubscribers[curPathIndex];
                PathSubscribers[curPathIndex] = subscriber - 1;
                AgentCurPathIndicies[i] = newPathIndex;
                AgentNewPathIndicies[i] = -1;
            }
            if (!finalReq.ReconstructionFlag)
            {
                AgentData agent = AgentDataArray[i];
                agent.ClearStatusBit(AgentStatus.HoldGround);
                agent.SetStatusBit(AgentStatus.Moving);
                AgentDestinationReachedArray[i] = false;
                AgentDataArray[i] = agent;
            }
        }
    }
}
