using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

[BurstCompile]
struct AgentDirectionSetJob : IJob
{
    [ReadOnly] public NativeArray<AgentMovementData> MovementDataArray;
    public NativeArray<AgentData> AgentDataDataArray;
    public void Execute()
    {
        for (int i = 0; i < MovementDataArray.Length; i++)
        {
            AgentMovementData movData = MovementDataArray[i];
            AgentData agentData = AgentDataDataArray[i];
            agentData.Direction = movData.Direction;
            AgentDataDataArray[i] = agentData;
        }
    }
}