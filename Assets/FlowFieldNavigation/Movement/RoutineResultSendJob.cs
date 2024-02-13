using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
struct RoutineResultSendJob : IJob
{
    [ReadOnly] internal NativeArray<AgentMovementData> MovementDataArray;
    [ReadOnly] internal NativeArray<RoutineResult> RoutineResultArray;
    [ReadOnly] internal NativeArray<int> NormalToHashed;
    [ReadOnly] internal NativeArray<int> AgentCurPathIndicies;
    [ReadOnly] internal NativeArray<bool> AgentDestinationReachedArray;
    internal NativeArray<AgentData> AgentDataArray;
    public void Execute()
    {
        for (int i = 0; i < RoutineResultArray.Length; i++)
        {
            AgentData agentData = AgentDataArray[i];
            int hashedIndex = NormalToHashed[i];
            AgentMovementData movementData = MovementDataArray[hashedIndex];
            RoutineResult result = RoutineResultArray[hashedIndex];
            AgentStatus newAgentStatus = AgentDestinationReachedArray[i] ? ~(~agentData.Status | AgentStatus.Moving) : agentData.Status;
            agentData.DirectionWithHeigth = result.NewDirection3;
            agentData.DesiredDirection = movementData.DesiredDirection;
            agentData.Direction = result.NewDirection;
            agentData.Seperation = result.NewSeperation;
            agentData.Avoidance = result.NewAvoidance;
            agentData.MovingAvoidance = result.NewMovingAvoidance;
            agentData.SplitInfo = result.NewSplitInfo;
            agentData.SplitInterval = result.NewSplitInterval;
            agentData.Status = newAgentStatus;
            AgentDataArray[i] = agentData;
        }
    }
}