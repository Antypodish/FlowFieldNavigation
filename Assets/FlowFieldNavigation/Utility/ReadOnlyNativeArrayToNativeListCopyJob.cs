using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;

namespace FlowFieldNavigation
{
    [BurstCompile]
    internal struct ReadOnlyNativeArrayToNativeListCopyJob<T> : IJob where T : unmanaged
    {
        internal NativeArray<T>.ReadOnly Source;
        internal NativeList<T> Destination;
        public void Execute()
        {
            Destination.Length = Source.Length;
            NativeArray<T> destinationAsArray = Destination.AsArray();
            for (int i = 0; i < Source.Length; i++)
            {
                destinationAsArray[i] = Source[i];
            }
        }
    }

}

