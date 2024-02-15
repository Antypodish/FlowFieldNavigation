using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;

namespace FlowFieldNavigation
{

    [BurstCompile]
    internal struct NativeListCopyJob<T> : IJob where T : unmanaged
    {
        [WriteOnly] internal NativeList<T> Destination;
        [ReadOnly] internal NativeList<T> Source;
        public void Execute()
        {
            int length = Source.Length;
            Destination.Length = length;
            for (int i = 0; i < length; i++)
            {
                Destination[i] = Source[i];
            }
        }
    }

}