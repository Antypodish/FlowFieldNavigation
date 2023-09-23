using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
struct RoutineResultSendJob : IJob
{
    [ReadOnly] public NativeArray<AgentMovementData> MovementDataArray;
    [ReadOnly] public NativeArray<float2> AgentVelocities;
    public NativeArray<AgentData> AgentDataArray;
    public void Execute()
    {
        for (int i = 0; i < AgentVelocities.Length; i++)
        {
            AgentData agentData = AgentDataArray[i];
            AgentMovementData movementData = MovementDataArray[i];
            agentData.waypoint = agentData.Destination.Equals(movementData.Destination) ? movementData.Waypoint : new Waypoint();
            agentData.Velocity = AgentVelocities[i];
            AgentDataArray[i] = agentData;
        }
    }
}