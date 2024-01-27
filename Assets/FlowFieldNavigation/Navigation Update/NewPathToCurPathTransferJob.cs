using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;

[BurstCompile]
internal struct NewPathToCurPathTransferJob : IJob
{
    internal NativeArray<int> AgentCurPathIndicies;
    internal NativeArray<int> AgentNewPathIndicies;
    internal NativeArray<int> PathSubscribers;
    [ReadOnly] internal NativeArray<FinalPathRequest> FinalPathRequests;
    [ReadOnly] internal NativeArray<int> AgentsToTryUnsubCurPath;
    [ReadOnly] internal NativeArray<int> AgentsToTrySubNewPath;
    public void Execute()
    {
        for (int i = 0; i < AgentsToTryUnsubCurPath.Length; i++)
        {
            int agentIndex = AgentsToTryUnsubCurPath[i];
            int curPathIndex = AgentCurPathIndicies[agentIndex];
            if (curPathIndex != -1)
            {
                int subscriber = PathSubscribers[curPathIndex];
                PathSubscribers[curPathIndex] = subscriber - 1;
                AgentCurPathIndicies[agentIndex] = -1;
            }
        }
        for (int i = 0; i < AgentsToTrySubNewPath.Length; i++)
        {
            int agentIndex = AgentsToTrySubNewPath[i];
            int newPathIndex = AgentNewPathIndicies[agentIndex];
            if (newPathIndex != -1)
            {
                FinalPathRequest finalReq = FinalPathRequests[newPathIndex];
                newPathIndex = finalReq.PathIndex;
                AgentCurPathIndicies[agentIndex] = newPathIndex;
                AgentNewPathIndicies[agentIndex] = -1;
            }
        }
    }
}
