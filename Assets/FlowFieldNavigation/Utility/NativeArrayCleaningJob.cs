using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;


namespace FlowFieldNavigation
{
    [BurstCompile]
    internal struct NativeArrayCleaningJob<T> : IJob where T : unmanaged
    {
        internal NativeArray<T> Array;
        public void Execute()
        {
            T defaultValue = default(T);
            for (int i = 0; i < Array.Length; i++)
            {
                Array[i] = defaultValue;
            }
        }
    }


}