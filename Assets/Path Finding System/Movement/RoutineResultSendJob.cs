using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
struct RoutineResultSendJob : IJob
{
    [ReadOnly] public NativeArray<AgentMovementData> MovementDataArray;
    [ReadOnly] public NativeArray<RoutineResult> RoutineResultArray;
    [ReadOnly] public NativeArray<int> NormalToHashed;
    [ReadOnly] public NativeArray<int> AgentCurPathIndicies;
    [ReadOnly] public NativeArray<bool> AgentDestinationReachedArray;
    public NativeArray<AgentData> AgentDataArray;
    public void Execute()
    {
        for (int i = 0; i < RoutineResultArray.Length; i++)
        {
            AgentData agentData = AgentDataArray[i];
            int hashedIndex = NormalToHashed[i];
            AgentMovementData movementData = MovementDataArray[hashedIndex];
            RoutineResult result = RoutineResultArray[hashedIndex];
            agentData.DesiredDirection = movementData.DesiredDirection;
            agentData.Direction = result.NewDirection;
            agentData.Seperation = result.NewSeperation;
            agentData.Avoidance = result.NewAvoidance;
            agentData.MovingAvoidance = result.NewMovingAvoidance;
            agentData.SplitInfo = result.NewSplitInfo;
            agentData.SplitInterval = result.NewSplitInterval;
            agentData.Status = AgentDestinationReachedArray[i] ? 0 : agentData.Status;
            AgentDataArray[i] = agentData;
        }
    }
}