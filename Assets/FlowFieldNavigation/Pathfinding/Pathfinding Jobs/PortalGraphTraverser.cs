using Unity.Collections;
using Unity.Mathematics;

namespace FlowFieldNavigation
{
    internal struct PortalGraphTraverser
    {
        NativeArray<PortalTraversalData> _portalTraversalDataArray;
        NativeArray<PortalNode>.ReadOnly _portalNodes;
        NativeArray<PortalToPortal>.ReadOnly _portalToPortals;
    }
}
