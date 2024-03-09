using Unity.Collections;

namespace FlowFieldNavigation
{
    internal struct PortalNeighbourEnumerator
    {
        NativeSlice<PortalToPortal> _regularNeighbours1;
        NativeSlice<PortalToPortal> _regularNeighbours2;
        PortalToPortal _destinationAsNeighbour;

    }
}
