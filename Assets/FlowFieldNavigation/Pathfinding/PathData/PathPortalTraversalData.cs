using Unity.Collections;


namespace FlowFieldNavigation
{
    internal struct PathPortalTraversalData
    {
        internal NativeList<PortalTraversalData> GoalDataList;
        internal NativeList<ActivePortal> PortalSequence;
        internal NativeList<int> PortalSequenceBorders;
        internal NativeList<int> TargetSectorPortalIndexList;
        internal NativeList<int> SourcePortalIndexList;
        internal NativeReference<int> PathAdditionSequenceBorderStartIndex;
        internal NativeReference<int> NewPickedSectorStartIndex;
        internal NativeList<int> DiskstraStartIndicies;
        internal NativeList<int> NewReducedPortalIndicies;
        internal NativeList<PortalTraversalDataRecord> PortalDataRecords;
    }


}