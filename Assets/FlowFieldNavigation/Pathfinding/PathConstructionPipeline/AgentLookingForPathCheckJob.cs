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
    internal NativeArray<bool> AgentLookingForPathFlags;
    internal NativeArray<int> AgentCurPathIndicies;
    public void Execute()
    {
        for(int agentIndex = 0; agentIndex < AgentLookingForPathFlags.Length; agentIndex++)
        {
            bool flag = AgentLookingForPathFlags[agentIndex];
            if (!flag) { continue; }

            AgentData agentData = AgentDataArray[agentIndex];
            float2 agentPos2 = new float2(agentData.Position.x, agentData.Position.z);
            int agentOffset = FlowFieldUtilities.RadiusToOffset(agentData.Radius, TileSize);
            int2 agentIndex2d = FlowFieldUtilities.PosTo2D(agentPos2, TileSize, FieldGridStartPos);
            LocalIndex1d agentLocal = FlowFieldUtilities.GetLocal1D(agentIndex2d, SectorColAmount, SectorMatrixColAmount);
            byte agentIndexCost = CostFields[agentOffset][agentLocal.sector * SectorTileAmount + agentLocal.index];
            if(agentIndexCost == byte.MaxValue) { continue; }

            IslandFieldProcessor islandFieldProcessor = IslandFieldProcessors[agentOffset];
            int agentIsland = islandFieldProcessor.GetIsland(agentIndex2d);
            int agentFlock = AgentFlockIndicies[agentIndex];

            for(int pathIndex = 0; pathIndex < PathStateArray.Length; pathIndex++)
            {
                if (PathStateArray[pathIndex] == PathState.Removed) { continue; }
                int pathFlock = PathFlockIndicies[pathIndex];
                if(pathFlock != agentFlock) { continue; }
                PathDestinationData destinationData = PathDestinationDataArray[pathIndex];
                if(agentOffset != destinationData.Offset) { continue; }
                int destinationIsland = islandFieldProcessor.GetIsland(destinationData.Destination);
                if(destinationIsland != agentIsland) { continue; }
                PathRoutineData routineData = PathRoutineDataArray[pathIndex];
                if(routineData.ReconstructionRequestIndex != -1) { continue; }

                PathSubscriberCounts[pathIndex]++;
                AgentCurPathIndicies[agentIndex] = pathIndex;
                AgentLookingForPathFlags[agentIndex] = false;
                break;
            }
        }
    }
}
//Agents cant do anything if there is not an existing path which conforms to their requirements
//Cannot handle paths being reconstructed at the moment
//Does not handle dynamic paths
