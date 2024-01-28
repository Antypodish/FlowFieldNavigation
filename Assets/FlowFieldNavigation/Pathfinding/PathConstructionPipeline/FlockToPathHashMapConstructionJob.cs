using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;

[BurstCompile]
internal struct FlockToPathHashMapConstructionJob : IJob
{
    internal int FlockListLength;
    internal NativeList<FlockSlice> FlockSlices;
    [ReadOnly] internal NativeList<int> ReadyAgentsLookingForPath;
    [ReadOnly] internal NativeArray<int> AgentFlockIndicies;
    [ReadOnly] internal NativeArray<int> PathFlockIndicies;
    [ReadOnly] internal NativeArray<PathState> PathStates;
    internal NativeList<int> PathIndicies;
    public void Execute()
    {
        NativeArray<int> ReadyAgentsLookingForPathAsArray = ReadyAgentsLookingForPath.AsArray();
        //Reset flock slices
        FlockSlices.Length = FlockListLength;
        for(int i = 0; i < FlockSlices.Length; i++)
        {
            FlockSlices[i] = new FlockSlice()
            {
                PathStart = -1,
                PathLength = 0,
            };
        }

        //Mark flocks
        //If start == 0, marked. If start == -1, not marked
        for(int i = 0; i < ReadyAgentsLookingForPathAsArray.Length; i++)
        {
            int agentIndex = ReadyAgentsLookingForPathAsArray[i];
            int flockIndex = AgentFlockIndicies[agentIndex];
            FlockSlices[flockIndex] = new FlockSlice()
            {
                PathStart = 0,
                PathLength = 0,
            };
        }

        //Submit Lengths
        int totalLength = 0;
        for(int pathIndex = 0; pathIndex < PathFlockIndicies.Length; pathIndex++)
        {
            if (PathStates[pathIndex] == PathState.Removed) { continue; }
            int pathFlockIndex = PathFlockIndicies[pathIndex];
            FlockSlice flockSlice = FlockSlices[pathFlockIndex];
            if(flockSlice.PathStart == -1) { continue; }
            flockSlice.PathLength++;
            FlockSlices[pathFlockIndex] = flockSlice;
            totalLength++;
        }
        PathIndicies.Length = totalLength;

        //Set flock slice starts
        totalLength = 0;
        for(int i = 0; i < FlockSlices.Length; i++)
        {
            FlockSlice flockSlice = FlockSlices[i];
            if(flockSlice.PathStart == -1) { continue; }
            flockSlice.PathStart = totalLength;
            totalLength += flockSlice.PathLength;
            flockSlice.PathLength = 0;
            FlockSlices[i] = flockSlice;
        }

        //Set path indicies
        for(int pathIndex = 0; pathIndex < PathFlockIndicies.Length; pathIndex++)
        {
            if (PathStates[pathIndex] == PathState.Removed) { continue; }
            int flockIndex = PathFlockIndicies[pathIndex];
            FlockSlice flockSlice = FlockSlices[flockIndex];
            if(flockSlice.PathStart == -1) { continue; }
            PathIndicies[flockSlice.PathStart + flockSlice.PathLength] = pathIndex;
            flockSlice.PathLength++;
            FlockSlices[flockIndex] = flockSlice;
        }
    }
}
internal struct FlockSlice
{
    internal int PathStart;
    internal int PathLength;
}