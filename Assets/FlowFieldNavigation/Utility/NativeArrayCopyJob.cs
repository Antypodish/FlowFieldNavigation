using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;

namespace FlowFieldNavigation
{
    [BurstCompile]
    internal struct NativeArrayCopyJob<T> : IJob where T : unmanaged
    {
        internal NativeArray<T> Source;
        internal NativeArray<T> Destination;
        public void Execute()
        {
            Destination.CopyFrom(Source);
        }
    }
}
