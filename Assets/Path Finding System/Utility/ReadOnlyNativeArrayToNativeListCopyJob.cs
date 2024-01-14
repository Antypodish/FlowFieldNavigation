using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;

[BurstCompile]
public struct ReadOnlyNativeArrayToNativeListCopyJob<T> : IJob where T : unmanaged
{
    public NativeArray<T>.ReadOnly Source;
    public NativeList<T> Destination;
    public void Execute()
    {
        Destination.Length = Source.Length;
        NativeArray<T> destinationAsArray = Destination;
        for(int i = 0; i < Source.Length; i++)
        {
            destinationAsArray[i] = Source[i];
        }
    }
}
