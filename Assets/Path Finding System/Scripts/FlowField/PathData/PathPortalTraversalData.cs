using Unity.Collections;

public struct PathPortalTraversalData
{
    public NativeArray<PortalTraversalData> PortalTraversalDataArray;
    public NativeList<ActivePortal> PortalSequence;
    public NativeList<int> PortalSequenceBorders;
    public NativeList<int> AStartTraverseIndexList;
    public NativeList<int> TargetSectorPortalIndexList;
    public NativeList<int> SourcePortalIndexList;
    public NativeArray<int> PathAdditionSequenceBorderStartIndex;
    public NativeArray<int> NewPickedSectorStartIndex;

    public void Dispose()
    {
        PathAdditionSequenceBorderStartIndex.Dispose();
        NewPickedSectorStartIndex.Dispose();
    }
}
