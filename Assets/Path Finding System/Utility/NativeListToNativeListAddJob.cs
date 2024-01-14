using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;

[BurstCompile]
public struct NativeListToNativeListAddJob<T> : IJob where T : unmanaged
{
    public NativeList<T> Source;
    public NativeList<T> Destination;
    public void Execute()
    {
        int initialSize = Destination.Length;
        Destination.Resize(Destination.Length + Source.Length, NativeArrayOptions.ClearMemory);
        for(int i = 0; i < Source.Length; i++)
        {
            Destination[initialSize + i] = Source[i];
        }
    }
}
