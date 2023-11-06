using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;

[BurstCompile]
public struct NativeListCopyJob<T> : IJob where T : unmanaged
{
    public NativeList<T> Destination;
    public NativeList<T> Source;
    public void Execute()
    {
        int length = Source.Length;
        Destination.Length = length;
        NativeArray<T> destination = Destination;
        NativeArray<T> source = Source;
        for(int i = 0; i < length; i++)
        {
            destination[i] = source[i];
        }
    }
}