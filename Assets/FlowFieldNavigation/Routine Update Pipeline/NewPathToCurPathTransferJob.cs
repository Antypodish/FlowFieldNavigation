using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;

[BurstCompile]
public struct NewPathToCurPathTransferJob : IJob
{
    public NativeArray<AgentData> AgentDataArray;
    public NativeArray<int> AgentCurPathIndicies;
    public NativeArray<int> AgentNewPathIndicies;
    public NativeArray<int> PathSubscribers;
    public NativeArray<bool> AgentDestinationReachedArray;
    public void Execute()
    {
        for (int i = 0; i < AgentCurPathIndicies.Length; i++)
        {
            int curPathIndex = AgentCurPathIndicies[i];
            int newPathIndex = AgentNewPathIndicies[i];
            if (newPathIndex == -1) { continue; }
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
            AgentData agent = AgentDataArray[i];
            agent.ClearStatusBit(AgentStatus.HoldGround);
            agent.SetStatusBit(AgentStatus.Moving);
            AgentDestinationReachedArray[i] = false;
            AgentDataArray[i] = agent;
        }
    }
}
