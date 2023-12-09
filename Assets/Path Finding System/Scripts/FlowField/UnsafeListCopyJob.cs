using Unity.Jobs;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
[BurstCompile]
public struct UnsafeListCopyJob<T> : IJob where T : unmanaged
{
    public UnsafeList<T> Source;
    public UnsafeList<T> Destination;
    public void Execute()
    {
        int minLenght = math.min(Source.Length, Destination.Length);
        for(int i = 0; i < minLenght; i++)
        {
            Destination[i] = Source[i];
        }
    }
}