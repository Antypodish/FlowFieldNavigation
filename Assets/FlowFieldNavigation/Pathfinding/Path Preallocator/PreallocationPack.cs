using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace FlowFieldNavigation
{

    internal struct PreallocationPack
    {
        internal NativeList<ActivePortal> PortalSequence;
        internal UnsafeList<float> TargetSectorCosts;
        internal NativeList<int> PickedToSector;
        internal NativeQueue<int> PortalTraversalFastMarchingQueue;
        internal NativeList<int> TargetSectorPortalIndexList;
        internal NativeList<int> SourcePortalIndexList;
        internal UnsafeList<PathSectorState> SectorStateTable;
        internal NativeList<int> SectorStartIndexListToCalculateIntegration;
        internal NativeList<int> SectorStartIndexListToCalculateFlow;
        internal NativeList<NotActivePortalRecord> NotActivePortalList;
        internal NativeReference<int> NewPickedSectorStartIndex;
        internal NativeReference<int> PathAdditionSequenceBorderStartIndex;
        internal UnsafeList<FlowData> DynamicAreaFlowFieldCalculationBuffer;
        internal NativeList<IntegrationTile> DynamicAreaIntegrationField;
        internal UnsafeList<SectorFlowStart> DynamicAreaSectorFlowStartCalculationList;
        internal NativeReference<SectorsWihinLOSArgument> SectorsWithinLOSState;
        internal SectorBitArray SectorBitArray;
        internal NativeList<int> DijkstraStartIndicies;
    }


}