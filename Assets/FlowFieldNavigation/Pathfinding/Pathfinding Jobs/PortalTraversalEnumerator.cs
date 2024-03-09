using Unity.Collections;

namespace FlowFieldNavigation
{
    internal struct PortalTraversalNeighbourEnumerator
    {
        NativeSlice<PortalTraversalData> PortalTraversalDataSlice;
        NativeSlice<int> _regularNeigbours;
        int _targetPointer;
        int _currentIndex;

        //Move next neighbour
        //Get neighbour index
    }
}
