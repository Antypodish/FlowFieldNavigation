using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;
using Unity.Mathematics;


namespace FlowFieldNavigation
{
    [BurstCompile]
    internal struct AgentLookingForPathCheckJob : IJob
    {
        internal float TileSize;
        internal int SectorColAmount;
        internal int SectorMatrixColAmount;
        internal int SectorTileAmount;
        internal float2 FieldGridStartPos;
        [ReadOnly] internal NativeArray<IslandFieldProcessor> IslandFieldProcessors;
        [ReadOnly] internal NativeArray<PathRoutineData> PathRoutineDataArray;
        [ReadOnly] internal NativeArray<PathDestinationData> PathDestinationDataArray;
        [ReadOnly] internal NativeArray<int> AgentFlockIndicies;
        [ReadOnly] internal NativeArray<float> AgentRadii;
        [ReadOnly] internal NativeArray<float3> AgentPositions;
        [ReadOnly] internal NativeList<int> ReadyAgentsLookingForPath;
        [ReadOnly] internal NativeList<PathRequestRecord> ReadyAgentsLookingForPathRequestRecords;
        [ReadOnly] internal FlockToPathHashMap FlockToPathHashmap;
        internal NativeArray<int> PathSubscriberCounts;
        internal NativeList<PathRequest> InitialPathRequests;
        internal NativeArray<int> AgentNewPathIndicies;
        internal NativeHashMap<int, int> FlockIndexToPathRequestIndex;
        internal NativeList<AgentAndPath> AgentIndiciesToSubExistingPath;
        public void Execute()
        {
            NativeArray<int> ReadyAgentsLookingForPathAsArray = ReadyAgentsLookingForPath.AsArray();
            for (int i = ReadyAgentsLookingForPathAsArray.Length - 1; i >= 0; i--)
            {
                int agentIndex = ReadyAgentsLookingForPathAsArray[i];
                PathRequestRecord requestRecord = ReadyAgentsLookingForPathRequestRecords[i];

                float3 agentpos3 = AgentPositions[agentIndex];
                float2 agentPos2 = new float2(agentpos3.x, agentpos3.z);
                int agentOffset = FlowFieldUtilities.RadiusToOffset(AgentRadii[agentIndex], TileSize);
                int2 agentIndex2d = FlowFieldUtilities.PosTo2D(agentPos2, TileSize, FieldGridStartPos);

                IslandFieldProcessor islandFieldProcessor = IslandFieldProcessors[agentOffset];
                int agentIsland = islandFieldProcessor.GetIsland(agentIndex2d);
                int agentFlock = AgentFlockIndicies[agentIndex];

                bool existingPathSuccesfull = TryFindingExistingPath(agentIndex, agentFlock, agentOffset, agentIsland, islandFieldProcessor);
                if (!existingPathSuccesfull)
                {
                    if (FlockIndexToPathRequestIndex.TryGetValue(agentFlock, out int pathRequestIndex))
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
            }
        }

        bool TryFindingExistingPath(int agentIndex, int agentFlock, int agentOffset, int agentIsland, IslandFieldProcessor islandFieldProcessor)
        {
            NativeSlice<int> pathIndicies = FlockToPathHashmap.GetPathIndiciesOfFlock(agentFlock);

            for (int i = 0; i < pathIndicies.Length; i++)
            {
                int pathIndex = pathIndicies[i];
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
            return false;/*
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
        return false;*/
        }

    }
    //Very naive approach O(m*n). Searches all paths for each agent in the list. Make it O(n).

}