using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

[BurstCompile]
internal struct AgentStartMovingJob : IJob
{
    [ReadOnly] internal NativeArray<int> AgentIndiciesToStartMoving;
    internal NativeArray<AgentData> AgentDataArray;
    internal NativeArray<bool> AgentDestinationReachedArray;
    public void Execute()
    {
        for(int i = 0; i < AgentIndiciesToStartMoving.Length; i++)
        {
            int agentIndex = AgentIndiciesToStartMoving[i];
            AgentData agent = AgentDataArray[agentIndex];
            agent.ClearStatusBit(AgentStatus.HoldGround);
            agent.SetStatusBit(AgentStatus.Moving);
            AgentDataArray[agentIndex] = agent;

            AgentDestinationReachedArray[agentIndex] = false;
        }
    }
}
