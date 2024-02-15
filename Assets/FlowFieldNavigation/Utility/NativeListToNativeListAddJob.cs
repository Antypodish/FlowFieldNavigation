using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;

namespace FlowFieldNavigation
{
    [BurstCompile]
    internal struct NativeListToNativeListAddJob<T> : IJob where T : unmanaged
    {
        internal NativeList<T> Source;
        internal NativeList<T> Destination;
        public void Execute()
        {
            int initialSize = Destination.Length;
            Destination.Resize(Destination.Length + Source.Length, NativeArrayOptions.ClearMemory);
            for (int i = 0; i < Source.Length; i++)
            {
                Destination[initialSize + i] = Source[i];
            }
        }
    }

}