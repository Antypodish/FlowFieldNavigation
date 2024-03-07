using System;

namespace FlowFieldNavigation
{
    [Flags]
    internal enum PortalTraversalMark : byte
    {
        AStarTraversed = 1,
        AStarExtracted = 2,
        AStarPicked = 4,
        DijkstraTraversed = 8,
        DijkstraPicked = 16,
        DijstraExtracted = 32,
        TargetNeighbour = 64,
        Reduced = 128,
    }

}

