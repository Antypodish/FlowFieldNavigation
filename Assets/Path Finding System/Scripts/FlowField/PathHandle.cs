using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
public struct PathHandle
{
    public JobHandle Handle;
    public int PathIndex;
    public NativeSlice<float2> Soruces;
}