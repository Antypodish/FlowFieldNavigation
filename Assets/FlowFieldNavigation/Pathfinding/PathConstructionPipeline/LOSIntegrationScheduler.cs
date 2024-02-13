using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
internal class LOSIntegrationScheduler
{
    PathfindingManager _pathfindingManager;
    PathDataContainer _pathContainer;

    NativeList<PathPipelineInfoWithHandle> ScheduledLOS;
    NativeList<int> _losCalculatedPaths;
    NativeList<JobHandle> _transferHandles;

    internal LOSIntegrationScheduler(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
        _pathContainer = pathfindingManager.PathDataContainer;
        ScheduledLOS = new NativeList<PathPipelineInfoWithHandle>(Allocator.Persistent);
        _losCalculatedPaths = new NativeList<int>(Allocator.Persistent);
        _transferHandles = new NativeList<JobHandle>(Allocator.Persistent);
    }
    internal void DisposeAll()
    {
        if (ScheduledLOS.IsCreated) { ScheduledLOS.Dispose(); }
        if (_losCalculatedPaths.IsCreated) { _losCalculatedPaths.Dispose(); }
        if (_transferHandles.IsCreated) { _transferHandles.Dispose(); }
    }

    internal void ScheduleLOS(PathPipelineInfoWithHandle pathInfo, JobHandle flowHandle = new JobHandle())
    {
        PathfindingInternalData internalData = _pathContainer.PathfindingInternalDataList[pathInfo.PathIndex];
        PathDestinationData destinationData = _pathContainer.PathDestinationDataList[pathInfo.PathIndex];
        PathLocationData locationData = _pathContainer.PathLocationDataList[pathInfo.PathIndex];
        int2 targetIndex = FlowFieldUtilities.PosTo2D(destinationData.Destination, FlowFieldUtilities.TileSize, FlowFieldUtilities.FieldGridStartPosition);
        CostField pickedCostField = _pathfindingManager.FieldDataContainer.GetCostFieldWithOffset(destinationData.Offset);

        JobHandle losHandle = flowHandle;
        bool requestedSectorWithinLOS = (internalData.SectorWithinLOSState.Value & SectorsWihinLOSArgument.RequestedSectorWithinLOS) == SectorsWihinLOSArgument.RequestedSectorWithinLOS;
        bool addedSectorWithinLOS = (internalData.SectorWithinLOSState.Value & SectorsWihinLOSArgument.AddedSectorWithinLOS) == SectorsWihinLOSArgument.AddedSectorWithinLOS;
        bool losCalculated = _pathContainer.IsLOSCalculated(pathInfo.PathIndex);
        bool destinationMoved = pathInfo.DestinationState == DynamicDestinationState.Moved;
        if (losCalculated && (addedSectorWithinLOS || destinationMoved))
        {
            LOSCleanJob losClean = new LOSCleanJob()
            {
                SectorColAmount = FlowFieldUtilities.SectorColAmount,
                SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
                SectorMatrixRowAmount = FlowFieldUtilities.SectorMatrixRowAmount,
                SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
                LOSRange = FlowFieldUtilities.LOSRange,
                Target = targetIndex,

                SectorToPickedTable = locationData.SectorToPicked,
                IntegrationField = internalData.IntegrationField.AsArray(),
            };
            JobHandle loscleanHandle = losClean.Schedule(flowHandle);

            LOSIntegrationJob losjob = new LOSIntegrationJob()
            {
                SectorColAmount = FlowFieldUtilities.SectorColAmount,
                SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
                SectorMatrixRowAmount = FlowFieldUtilities.SectorMatrixRowAmount,
                SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
                FieldColAmount = FlowFieldUtilities.FieldColAmount,
                MaxLOSRange = FlowFieldUtilities.LOSRange,
                TileSize = FlowFieldUtilities.TileSize,
                FieldRowAmount = FlowFieldUtilities.FieldRowAmount,

                Costs = pickedCostField.Costs,
                SectorToPicked = locationData.SectorToPicked,
                IntegrationField = internalData.IntegrationField.AsArray(),
                Target = targetIndex,
            };
            losHandle = losjob.Schedule(loscleanHandle);
            _losCalculatedPaths.Add(pathInfo.PathIndex);
        }
        else if (requestedSectorWithinLOS)
        {
            LOSIntegrationJob losjob = new LOSIntegrationJob()
            {
                SectorColAmount = FlowFieldUtilities.SectorColAmount,
                SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
                SectorMatrixRowAmount = FlowFieldUtilities.SectorMatrixRowAmount,
                SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
                FieldColAmount = FlowFieldUtilities.FieldColAmount,
                MaxLOSRange = FlowFieldUtilities.LOSRange,
                TileSize = FlowFieldUtilities.TileSize,
                FieldRowAmount = FlowFieldUtilities.FieldRowAmount,

                Costs = pickedCostField.Costs,
                SectorToPicked = locationData.SectorToPicked,
                IntegrationField = internalData.IntegrationField.AsArray(),
                Target = targetIndex,
            };
            losHandle = losjob.Schedule(flowHandle);
            _losCalculatedPaths.Add(pathInfo.PathIndex);
        }
        internalData.SectorWithinLOSState.Value = SectorsWihinLOSArgument.None;

        if (FlowFieldUtilities.DebugMode) { losHandle.Complete(); }
        pathInfo.Handle = losHandle;
        ScheduledLOS.Add(pathInfo);
    }
    internal void TryComplete()
    {
        for (int i = ScheduledLOS.Length - 1; i >= 0; i--)
        {
            PathPipelineInfoWithHandle flowHandle = ScheduledLOS[i];
            if (flowHandle.Handle.IsCompleted)
            {
                flowHandle.Handle.Complete();
                ScheduledLOS.RemoveAtSwapBack(i);
            }
        }
    }
    internal void ForceComplete()
    {
        for (int i = ScheduledLOS.Length - 1; i >= 0; i--)
        {
            PathPipelineInfoWithHandle losHandle = ScheduledLOS[i];
            losHandle.Handle.Complete();
        }
        ScheduledLOS.Clear();
    }
    internal void ScheduleLOSTransfers()
    {
        List<PathfindingInternalData> internalDataList = _pathContainer.PathfindingInternalDataList;
        NativeList<PathLocationData> pathLocationDataList = _pathContainer.PathLocationDataList;
        NativeList<PathFlowData> pathFlowDataList = _pathContainer.PathFlowDataList;
        NativeList<PathDestinationData> pathDestinationDataList = _pathContainer.PathDestinationDataList;
        for (int i = 0; i < _losCalculatedPaths.Length; i++)
        {
            int pathIndex = _losCalculatedPaths[i];
            PathfindingInternalData internalData = internalDataList[pathIndex];
            PathDestinationData destinationData = pathDestinationDataList[pathIndex];
            PathLocationData locationData = pathLocationDataList[pathIndex];
            PathFlowData flowData = pathFlowDataList[pathIndex];
            LOSTransferJob losTransfer = new LOSTransferJob()
            {
                SectorColAmount = FlowFieldUtilities.SectorColAmount,
                SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
                SectorMatrixRowAmount = FlowFieldUtilities.SectorMatrixRowAmount,
                SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
                LOSRange = FlowFieldUtilities.LOSRange,
                SectorToPickedTable = locationData.SectorToPicked,
                LOSBitmap = flowData.LOSMap,
                IntegrationField = internalData.IntegrationField.AsArray(),
                Target = FlowFieldUtilities.PosTo2D(destinationData.Destination, FlowFieldUtilities.TileSize, FlowFieldUtilities.FieldGridStartPosition),
            };
            _transferHandles.Add(losTransfer.Schedule());
        }
        _losCalculatedPaths.Clear();
    }
    internal void CompleteLOSTransfers()
    {
        for(int i = 0; i < _transferHandles.Length; i++)
        {
            _transferHandles[i].Complete();
        }
        _transferHandles.Clear();
    }
}
