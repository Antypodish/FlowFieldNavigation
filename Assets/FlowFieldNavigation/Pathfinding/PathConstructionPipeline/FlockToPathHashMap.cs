using Unity.Collections;

internal struct FlockToPathHashMap
{
    internal NativeList<FlockSlice> FlockSlices;
    internal NativeList<int> PathIndicies;

    public NativeSlice<int> GetPathIndiciesOfFlock(int flockIndex)
    {
        if(flockIndex < 0 || flockIndex >= FlockSlices.Length) { return new NativeSlice<int>(PathIndicies.AsArray(), 0, 0); }
        FlockSlice flockSlice = FlockSlices[flockIndex];
        if(flockSlice.PathStart == -1 || flockSlice.PathLength == 0) { return new NativeSlice<int>(PathIndicies.AsArray(), 0, 0); }
        return new NativeSlice<int>(PathIndicies.AsArray(), flockSlice.PathStart, flockSlice.PathLength);
    }
}