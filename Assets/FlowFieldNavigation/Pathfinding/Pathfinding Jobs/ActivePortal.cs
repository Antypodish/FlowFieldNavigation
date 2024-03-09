using Unity.Burst;

namespace FlowFieldNavigation
{
    [BurstCompile]
    internal struct ActivePortal
    {
        internal int FieldIndex1;
        internal int FieldIndex2;
        internal float Distance;
    }

}

