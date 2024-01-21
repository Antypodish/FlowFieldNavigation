using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

internal struct PathfindingInternalData
{
    internal DynamicArea DynamicArea;
    internal NativeList<int> PickedSectorList;
    internal NativeList<IntegrationTile> IntegrationField;
    internal NativeArray<int> FlowFieldLength;
    internal NativeQueue<int> PortalTraversalQueue;
    internal NativeList<UnsafeList<ActiveWaveFront>> ActivePortalList;
    internal NativeList<int> NotActivePortalList;
    internal NativeList<int> SectorFlowStartIndiciesToCalculateIntegration;
    internal NativeList<int> SectorFlowStartIndiciesToCalculateFlow;
    internal NativeArray<SectorsWihinLOSArgument> SectorWithinLOSState;
    internal void Dispose()
    {
        NotActivePortalList.Dispose();
        SectorFlowStartIndiciesToCalculateFlow.Dispose();
        SectorFlowStartIndiciesToCalculateIntegration.Dispose();
        SectorWithinLOSState.Dispose();
        DynamicArea.Dispose();
    }
}

internal enum PathState : byte
{
    Clean = 0,
    ToBeUpdated = 1,
    ToBeDisposed = 2,
    Removed,
}
internal enum SectorsWihinLOSArgument : byte
{
    None = 0,
    RequestedSectorWithinLOS = 1,
    AddedSectorWithinLOS = 2,
};