using Unity.Burst;

namespace FlowFieldNavigation
{
    [BurstCompile]
    internal struct ActivePortal
    {
        internal int Index;
        internal int NextIndex;
        internal float Distance;

        internal bool IsTargetNode() => Index == -1 && Distance == 0 && NextIndex == -1;
        internal bool IsTargetNeighbour() => NextIndex == -1;
        internal static ActivePortal GetTargetNode()
        {
            return new ActivePortal()
            {
                Index = -1,
                Distance = 0,
                NextIndex = -1
            };
        }
    }

}

