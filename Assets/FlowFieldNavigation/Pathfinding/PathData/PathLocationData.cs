using Unity.Collections.LowLevel.Unsafe;


namespace FlowFieldNavigation
{
    internal struct PathLocationData
    {
        internal UnsafeList<SectorFlowStart> DynamicAreaPickedSectorFlowStarts;
    }

}