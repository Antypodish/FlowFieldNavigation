using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;

namespace FlowFieldNavigation
{
    internal struct NewPathHandle
    {
        internal JobHandle Handle;
        internal int PathIndex;
        internal NativeSlice<float2> Soruces;
    }

}