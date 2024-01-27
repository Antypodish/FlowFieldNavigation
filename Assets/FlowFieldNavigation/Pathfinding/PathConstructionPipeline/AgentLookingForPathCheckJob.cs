using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;
using Unity.Mathematics;

[BurstCompile]
internal struct AgentLookingForPathCheckJob : IJob
{
    internal float TileSize;
    internal int SectorColAmount;
    internal int SectorMatrixColAmount;
    internal int SectorTileAmount;
    internal float2 FieldGridStartPos;
    [ReadOnly] internal NativeArray<IslandFieldProcessor> IslandFieldProcessors;
    [ReadOnly] internal NativeArray<UnsafeListReadOnly<byte>> CostFields;
    [ReadOnly] internal NativeArray<int> PathFlockIndicies;
    [ReadOnly] internal NativeArray<PathRoutineData> PathRoutineDataArray;
    [ReadOnly] internal NativeArray<PathState> PathStateArray;
    [ReadOnly] internal NativeArray<PathDestinationData> PathDestinationDataArray;
    [ReadOnly] internal NativeArray<int> AgentFlockIndicies;
    [ReadOnly] internal NativeArray<AgentData> AgentDataArray;
    internal NativeArray<int> PathSubscriberCounts;
    internal NativeList<PathRequest> InitialPathRequests;
    internal NativeList<int> AgentsLookingForPath;
    internal NativeList<PathRequestRecord> AgentsLookingForPathRequestRecords;
    internal NativeArray<int> AgentCurPathIndicies;
    internal NativeArray<int> AgentNewPathIndicies;
    public void Execute()
    {
        for(int i = AgentsLookingForPath.Length -1; i >= 0; i--)
        {
            int agentIndex = AgentsLookingForPath[i];
            PathRequestRecord requestRecord = AgentsLookingForPathRequestRecords[i];

            AgentData agentData = AgentDataArray[agentIndex];
            float2 agentPos2 = new float2(agentData.Position.x, agentData.Position.z);
            int agentOffset = FlowFieldUtilities.RadiusToOffset(agentData.Radius, TileSize);
            int2 agentIndex2d = FlowFieldUtilities.PosTo2D(agentPos2, TileSize, FieldGridStartPos);
            LocalIndex1d agentLocal = FlowFieldUtilities.GetLocal1D(agentIndex2d, SectorColAmount, SectorMatrixColAmount);
            byte agentIndexCost = CostFields[agentOffset][agentLocal.sector * SectorTileAmount + agentLocal.index];
            if (agentIndexCost == byte.MaxValue) { continue; }

            IslandFieldProcessor islandFieldProcessor = IslandFieldProcessors[agentOffset];
            int agentIsland = islandFieldProcessor.GetIsland(agentIndex2d);
            int agentFlock = AgentFlockIndicies[agentIndex];

            bool existingPathSuccesfull = TryFindingExistingPath(agentIndex, agentFlock, agentOffset, agentIsland, islandFieldProcessor);
            if (!existingPathSuccesfull)
            {
                PathRequest newRequest = new PathRequest(requestRecord);
                AgentNewPathIndicies[agentIndex] = InitialPathRequests.Length;
                InitialPathRequests.Add(newRequest);
            }
            AgentsLookingForPath.RemoveAtSwapBack(i);
            AgentsLookingForPathRequestRecords.RemoveAtSwapBack(i);
        }
    }

    bool TryFindingExistingPath(int agentIndex, int agentFlock, int agentOffset, int agentIsland, IslandFieldProcessor islandFieldProcessor)
    {
        for (int pathIndex = 0; pathIndex < PathStateArray.Length; pathIndex++)
        {
            if (PathStateArray[pathIndex] == PathState.Removed) { continue; }
            int pathFlock = PathFlockIndicies[pathIndex];
            if (pathFlock != agentFlock) { continue; }
            PathDestinationData destinationData = PathDestinationDataArray[pathIndex];
            if (agentOffset != destinationData.Offset) { continue; }
            int destinationIsland = islandFieldProcessor.GetIsland(destinationData.Destination);
            if (destinationIsland != agentIsland) { continue; }
            PathRoutineData routineData = PathRoutineDataArray[pathIndex];
            if (routineData.ReconstructionRequestIndex != -1)
            {
                AgentNewPathIndicies[agentIndex] = routineData.ReconstructionRequestIndex;
            }
            else
            {
                PathSubscriberCounts[pathIndex]++;
                AgentCurPathIndicies[agentIndex] = pathIndex;
            }
            return true;
        }
        return false;
    }

}
//Requested paths in case of !existingPathSuccesfull do not consider other agents. Each agent request a unique one. Make them consider each other.
//Does not handle dynamic paths
