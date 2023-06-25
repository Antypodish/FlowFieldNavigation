using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

[BurstCompile]
public struct DirectionCalculationJob : IJob
{
    public UnsafeList<AgentMovementData> AgentMovementData;
    public void Execute()
    {

    }
}
