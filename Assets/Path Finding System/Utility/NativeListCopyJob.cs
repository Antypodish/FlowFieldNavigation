using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;

[BurstCompile]
public struct NativeListCopyJob<T> : IJob where T : unmanaged
{
    [WriteOnly] public NativeList<T> Destination;
    [ReadOnly] public NativeList<T> Source;
    public void Execute()
    {
        int length = Source.Length;
        Destination.Length = length;
        for(int i = 0; i < length; i++)
        {
            Destination[i] = Source[i];
        }
    }
}