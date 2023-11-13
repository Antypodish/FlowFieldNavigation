using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
public struct NewPathHandle
{
    public JobHandle Handle;
    public int PathIndex;
    public NativeSlice<float2> Soruces;
}