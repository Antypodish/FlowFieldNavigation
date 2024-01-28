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
    internal NativeArray<int> AgentNewPathIndicies;
    internal NativeHashMap<int, int> FlockIndexToPathRequestIndex;
    internal NativeList<AgentAndPath> AgentIndiciesToSubExistingPath;
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
                if(FlockIndexToPathRequestIndex.TryGetValue(agentFlock, out int pathRequestIndex))
                {
                    AgentNewPathIndicies[agentIndex] = pathRequestIndex;
                }
                else
                {
                    int newPathRequestIndex = InitialPathRequests.Length;
                    PathRequest newRequest = new PathRequest(requestRecord);
                    AgentNewPathIndicies[agentIndex] = newPathRequestIndex;
                    InitialPathRequests.Add(newRequest);
                    FlockIndexToPathRequestIndex.Add(agentFlock, newPathRequestIndex);
                }
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
            if (routineData.PathReconstructionFlag) { continue; }
            AgentAndPath agentAndPath = new AgentAndPath()
            {
                AgentIndex = agentIndex,
                PathIndex = pathIndex,
            };
            AgentIndiciesToSubExistingPath.Add(agentAndPath);
            return true;
        }
        return false;
    }

}
//Does not handle dynamic paths
//Very naive approach O(m*n). Searches all paths for each agent in the list. Make it O(n).