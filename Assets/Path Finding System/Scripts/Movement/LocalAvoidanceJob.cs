using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

public struct LocalAvoidanceJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<AgentMovementData> AgentMovementData;
    public NativeArray<float2> AgentDirections;

    public void Execute(int index)
    {
        for(int i = 0; i< AgentMovementData.Length; i++)
        {
            AgentMovementData mateData = AgentMovementData[i];
        }
        AgentDirections[index] = AgentMovementData[index].Flow;
    }
}
