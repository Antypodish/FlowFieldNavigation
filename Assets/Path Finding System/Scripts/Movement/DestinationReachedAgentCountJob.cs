using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct DestinationReachedAgentCountJob : IJob
{
    [ReadOnly] public NativeArray<bool> AgentDestinationReachStatus;
    [ReadOnly] public NativeArray<int> AgentFlockIndexArray;
    [ReadOnly] public NativeArray<AgentMovementData> AgentMovementDataArray;
    [ReadOnly] public NativeArray<int> NormalToHashed;
    public NativeArray<FlockDestinationReachData> FlockDestinationReachArray;
    public void Execute()
    {
        //CLEAR STOPPED AGENT COUNT
        for(int i = 0; i < FlockDestinationReachArray.Length; i++) { FlockDestinationReachArray[i] = new FlockDestinationReachData(); }

        //SET STOPPED AGENT COUNT
        for(int i = 0; i < AgentDestinationReachStatus.Length; i++)
        {
            bool isDestinationReached = AgentDestinationReachStatus[i];
            if (isDestinationReached)
            {
                int flockIndex = AgentFlockIndexArray[i];
                int hashedIndex = NormalToHashed[i];
                float agentRadius = AgentMovementDataArray[hashedIndex].Radius;
                FlockDestinationReachData reachData = FlockDestinationReachArray[flockIndex];
                reachData.ReachedAgentCount++;
                reachData.TotalOccupiedArea += math.PI * agentRadius * agentRadius;
                reachData.TotalAgentRadius += agentRadius;
                FlockDestinationReachArray[flockIndex] = reachData;
            }
        }
    }
}
public struct FlockDestinationReachData
{
    public int ReachedAgentCount;
    public float TotalOccupiedArea;
    public float TotalAgentRadius;
}