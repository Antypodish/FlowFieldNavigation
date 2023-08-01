using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
struct AgentDirectionSetJob : IJob
{
    [ReadOnly] public NativeArray<AgentMovementData> MovementDataArray;
    [ReadOnly] public NativeArray<float2> AgentDirections;
    public NativeArray<AgentData> AgentDataArray;
    public void Execute()
    {
        for (int i = 0; i < AgentDirections.Length; i++)
        {
            AgentData agentData = AgentDataArray[i];
            agentData.Direction = AgentDirections[i];
            agentData.waypoint = MovementDataArray[i].waypoint;
            AgentDataArray[i] = agentData;
        }
    }
}