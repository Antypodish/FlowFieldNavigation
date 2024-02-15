using Unity.Collections;


namespace FlowFieldNavigation
{
    internal struct PathPortalTraversalData
    {
        internal NativeArray<PortalTraversalData> PortalTraversalDataArray;
        internal NativeList<ActivePortal> PortalSequence;
        internal NativeList<int> PortalSequenceBorders;
        internal NativeList<int> AStartTraverseIndexList;
        internal NativeList<int> TargetSectorPortalIndexList;
        internal NativeList<int> SourcePortalIndexList;
        internal NativeReference<int> PathAdditionSequenceBorderStartIndex;
        internal NativeReference<int> NewPickedSectorStartIndex;
        internal NativeArray<DijkstraTile> TargetSectorCosts;
        internal NativeList<int> DiskstraStartIndicies;
    }


}