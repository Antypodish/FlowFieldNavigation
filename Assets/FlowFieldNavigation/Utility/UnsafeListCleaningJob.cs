using System;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace FlowFieldNavigation
{
    [BurstCompile]
    internal struct UnsafeListCleaningJob<T> : IJob where T : unmanaged
    {
        internal UnsafeList<T> List;
        public void Execute()
        {
            T defaultValue = default(T);
            for (int i = 0; i < List.Length; i++)
            {
                List[i] = defaultValue;
            }
        }
    }

}

