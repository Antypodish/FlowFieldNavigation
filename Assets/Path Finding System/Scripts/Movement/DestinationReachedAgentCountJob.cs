using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct DestinationReachedAgentCountJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<bool> AgentDestinationReachStatus;
    [ReadOnly] public NativeArray<float2> PathDestinationArray;
    [ReadOnly] public AgentSpatialHashGrid AgentSpatialHashGrid;
    [ReadOnly] public NativeArray<PathState> PathStateList;
    [ReadOnly] public NativeArray<int> PathFlockIndexArray;
    [ReadOnly] public NativeArray<int> HashedToNormal;
    [ReadOnly] public NativeArray<int> AgentFlockIndicies;
    public NativeArray<float> PathReachDistanceCheckRanges;
    public NativeArray<float> PathReachDistances;
    public void Execute(int index)
    {
        PathReachDistances[index] = 0;

        if (PathStateList[index] == PathState.Removed) { return; }

        int flockIndex = PathFlockIndexArray[index];
        float2 pathDestination = PathDestinationArray[index];
        float checkRange = PathReachDistanceCheckRanges[index];
        float totalOccupiedArea = 0;
        for(int i = 0; i < AgentSpatialHashGrid.GetGridCount(); i++)
        {
            SpatialHashGridIterator spatialIterator = AgentSpatialHashGrid.GetIterator(pathDestination, checkRange, i);
            while (spatialIterator.HasNext())
            {
                NativeSlice<AgentMovementData> agentsToCheck = spatialIterator.GetNextRow(out int sliceStartIndex);
                for(int j = 0; j < agentsToCheck.Length; j++)
                {
                    AgentMovementData agentData = agentsToCheck[j];
                    float2 agentPos = new float2(agentData.Position.x, agentData.Position.z);
                    float distance = math.distance(agentPos, pathDestination);
                    if(distance > checkRange + agentData.Radius) { continue; }
                    int normalIndex = HashedToNormal[sliceStartIndex + j];
                    if (!AgentDestinationReachStatus[normalIndex]) { continue; }
                    int agentFlockIndex = AgentFlockIndicies[normalIndex];
                    if (agentFlockIndex != flockIndex) { continue; }
                    totalOccupiedArea += math.PI * agentData.Radius * agentData.Radius;
                }
            }
        }
        float totalCheckRadius = math.sqrt(totalOccupiedArea / math.PI);
        if(checkRange < totalCheckRadius) { PathReachDistanceCheckRanges[index] = totalCheckRadius; }
        PathReachDistances[index] = totalCheckRadius;
    }
}