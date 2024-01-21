using Unity.Collections;

internal struct PathPortalTraversalData
{
    internal NativeArray<PortalTraversalData> PortalTraversalDataArray;
    internal NativeList<ActivePortal> PortalSequence;
    internal NativeList<int> PortalSequenceBorders;
    internal NativeList<int> AStartTraverseIndexList;
    internal NativeList<int> TargetSectorPortalIndexList;
    internal NativeList<int> SourcePortalIndexList;
    internal NativeArray<int> PathAdditionSequenceBorderStartIndex;
    internal NativeArray<int> NewPickedSectorStartIndex;
    internal NativeArray<DijkstraTile> TargetSectorCosts;

    internal void Dispose()
    {
        PathAdditionSequenceBorderStartIndex.Dispose();
        NewPickedSectorStartIndex.Dispose();
    }
}
