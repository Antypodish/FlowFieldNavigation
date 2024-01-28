using Unity.Collections;

internal struct FlockToPathHashMap
{
    internal NativeArray<FlockSlice> FlockSlices;
    internal NativeArray<int> PathIndicies;

    public NativeSlice<int> GetPathIndiciesOfFlock(int flockIndex)
    {
        FlockSlice flockSlice = FlockSlices[flockIndex];
        if(flockSlice.PathStart == -1 || flockSlice.PathLength == 0) { return new NativeSlice<int>(PathIndicies, 0, 0); }
        return new NativeSlice<int>(PathIndicies, flockSlice.PathStart, flockSlice.PathLength);
    }
}