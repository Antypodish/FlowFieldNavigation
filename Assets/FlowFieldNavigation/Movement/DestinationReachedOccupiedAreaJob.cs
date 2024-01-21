using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
internal struct DestinationReachedOccupiedAreaJob : IJob
{
    [ReadOnly] internal NativeArray<bool> AgentDestinationReachStatus;
    [ReadOnly] internal NativeArray<int> AgentFlockIndexArray;
    internal NativeArray<int> FlockStoppedAgentCountArray;
    public void Execute()
    {
        //CLEAR STOPPED AGENT COUNT
        for(int i = 0; i < FlockStoppedAgentCountArray.Length; i++) { FlockStoppedAgentCountArray[i] = 0; }

        //SET STOPPED AGENT COUNT
        for(int i = 0; i < AgentDestinationReachStatus.Length; i++)
        {
            bool isDestinationReached = AgentDestinationReachStatus[i];
            if (isDestinationReached)
            {
                int flockIndex = AgentFlockIndexArray[i];
                FlockStoppedAgentCountArray[flockIndex] += 1;
            }
        }
    }
}
