using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
struct AgentDirectionSetJob : IJob
{
    [ReadOnly] public NativeArray<float2> AgentDirections;
    public NativeArray<AgentData> AgentDataDataArray;
    public void Execute()
    {
        for (int i = 0; i < AgentDirections.Length; i++)
        {
            AgentData agentData = AgentDataDataArray[i];
            agentData.Direction = AgentDirections[i];
            AgentDataDataArray[i] = agentData;
        }
    }
}