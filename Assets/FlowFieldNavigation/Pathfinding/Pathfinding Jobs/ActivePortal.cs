using Unity.Burst;

namespace FlowFieldNavigation
{
    [BurstCompile]
    internal struct ActivePortal
    {
        internal int FieldIndex1;
        internal int FieldIndex2;
        internal int NextIndex;
        internal float Distance;

        internal bool IsTargetNeighbour() => NextIndex == -1;
    }

}

