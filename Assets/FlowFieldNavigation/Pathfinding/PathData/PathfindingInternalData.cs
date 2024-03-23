using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;


namespace FlowFieldNavigation
{
    internal struct PathfindingInternalData
    {
        internal DynamicArea DynamicArea;
        internal NativeList<int> PickedSectorList;
        internal NativeList<IntegrationTile> IntegrationField;
        internal NativeQueue<int> PortalTraversalQueue;
        internal NativeParallelMultiHashMap<int, ActiveWaveFront> SectorToWaveFrontsMap;
        internal NativeList<NotActivePortalRecord> NotActivePortalList;
        internal NativeList<int> SectorIndiciesToCalculateIntegration;
        internal NativeList<int> SectorIndiciesToCalculateFlow;
        internal NativeReference<SectorsWihinLOSArgument> SectorWithinLOSState;
        internal NativeReference<bool> LOSCalculatedFlag;
        internal NativeList<FlowData> FlowFieldCalculationBuffer;
    }

    internal enum PathState : byte
    {
        Clean = 0,
        Removed,
    }
    internal enum SectorsWihinLOSArgument : byte
    {
        None = 0,
        RequestedSectorWithinLOS = 1,
        AddedSectorWithinLOS = 2,
    };

}