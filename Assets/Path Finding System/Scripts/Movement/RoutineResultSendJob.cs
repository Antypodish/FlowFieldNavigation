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
    public NativeArray<AgentData> AgentDataArray;
    public void Execute()
    {
        for (int i = 0; i < RoutineResultArray.Length; i++)
        {
            AgentData agentData = AgentDataArray[i];
            AgentMovementData movementData = MovementDataArray[NormalToHashed[i]];
            RoutineResult result = RoutineResultArray[NormalToHashed[i]];
            agentData.DesiredDirection = movementData.DesiredDirection;
            agentData.Direction = result.NewDirection;
            agentData.Seperation = result.NewSeperation;
            agentData.Avoidance = result.NewAvoidance;
            agentData.MovingAvoidance = result.NewMovingAvoidance;
            agentData.SplitInfo = result.NewSplitInfo;
            agentData.SplitInterval = result.NewSplitInterval;
            AgentDataArray[i] = agentData;
        }
    }
}