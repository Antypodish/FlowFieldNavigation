using Unity.Jobs;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace FlowFieldNavigation
{
    [BurstCompile]
    internal struct UnsafeListCopyJob<T> : IJob where T : unmanaged
    {
        internal UnsafeList<T> Source;
        internal UnsafeList<T> Destination;
        public void Execute()
        {
            int minLenght = math.min(Source.Length, Destination.Length);
            for (int i = 0; i < minLenght; i++)
            {
                Destination[i] = Source[i];
            }
        }
    }

}