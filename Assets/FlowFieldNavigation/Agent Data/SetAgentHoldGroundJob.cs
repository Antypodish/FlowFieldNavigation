using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;

[BurstCompile]
internal struct SetAgentHoldGroundJob : IJob
{
    [ReadOnly] internal NativeArray<int> AgentIndiciesToHoldGround;
    internal NativeArray<AgentData> AgentDataArray;

    public void Execute()
    {
        for(int i = 0; i < AgentIndiciesToHoldGround.Length; i++)
        {
            int agentIndex = AgentIndiciesToHoldGround[i];
            AgentData agentData = AgentDataArray[agentIndex];
            agentData.Status = AgentStatus.HoldGround;
            AgentDataArray[agentIndex] = agentData;
        }
    }
}