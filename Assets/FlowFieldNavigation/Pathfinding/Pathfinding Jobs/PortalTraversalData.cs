using Unity.Burst;

namespace FlowFieldNavigation
{
    [BurstCompile]
    internal struct PortalTraversalData
    {
        internal int OriginIndex;
        internal int NextIndex;
        internal float GCost;
        internal float HCost;
        internal float FCost;
        internal float DistanceFromTarget;
        internal PortalTraversalMark Mark;
        internal bool HasMark(PortalTraversalMark mark)
        {
            return (Mark & mark) == mark;
        }
    }

}

