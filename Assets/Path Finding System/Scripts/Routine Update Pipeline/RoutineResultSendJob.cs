using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
struct RoutineResultSendJob : IJob
{
    [ReadOnly] public NativeArray<AgentMovementData> MovementDataArray;
    [ReadOnly] public NativeArray<float2> AgentDirections;
    public NativeArray<AgentData> AgentDataArray;
    public void Execute()
    {
        for (int i = 0; i < AgentDirections.Length; i++)
        {
            AgentData agentData = AgentDataArray[i];
            AgentMovementData movementData = MovementDataArray[i];
            agentData.waypoint = agentData.Destination.Equals(movementData.Destination) ? movementData.Waypoint : new Waypoint();
            agentData.Direction = AgentDirections[i];
            agentData.Seperation = movementData.SeperationForce;
            agentData.Avoidance = agentData.Destination.Equals(movementData.Destination) ? movementData.Avoidance : AvoidanceStatus.None;
            agentData.SplitInfo = movementData.SplitInfo;
            agentData.SplitInterval = movementData.SplitInterval;
            AgentDataArray[i] = agentData;
        }
    }
}