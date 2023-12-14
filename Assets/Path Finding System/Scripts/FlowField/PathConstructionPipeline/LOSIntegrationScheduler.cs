using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Networking.Match;

public class LOSIntegrationScheduler
{
    PathfindingManager _pathfindingManager;
    PathContainer _pathContainer;

    NativeList<PathPipelineInfoWithHandle> ScheduledLOS;
    NativeList<int> _losCalculatedPaths;
    NativeList<JobHandle> _transferHandles;

    public LOSIntegrationScheduler(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
        _pathContainer = pathfindingManager.PathContainer;
        ScheduledLOS = new NativeList<PathPipelineInfoWithHandle>(Allocator.Persistent);
        _losCalculatedPaths = new NativeList<int>(Allocator.Persistent);
        _transferHandles = new NativeList<JobHandle>(Allocator.Persistent);
    }

    public void ScheduleLOS(PathPipelineInfoWithHandle pathInfo, JobHandle flowHandle = new JobHandle())
    {
        Path path = _pathContainer.ProducedPaths[pathInfo.PathIndex];
        PathDestinationData destinationData = _pathContainer.PathDestinationDataList[pathInfo.PathIndex];
        PathLocationData locationData = _pathContainer.PathLocationDataList[pathInfo.PathIndex];
        CostField pickedCostField = _pathfindingManager.FieldProducer.GetCostFieldWithOffset(path.Offset);

        JobHandle losHandle = flowHandle;
        bool requestedSectorWithinLOS = (path.SectorWithinLOSState[0] & SectorsWihinLOSArgument.RequestedSectorWithinLOS) == SectorsWihinLOSArgument.RequestedSectorWithinLOS;
        bool addedSectorWithinLOS = (path.SectorWithinLOSState[0] & SectorsWihinLOSArgument.AddedSectorWithinLOS) == SectorsWihinLOSArgument.AddedSectorWithinLOS;
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
                Target = destinationData.TargetIndex,

                SectorToPickedTable = locationData.SectorToPicked,
                IntegrationField = path.IntegrationField,
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

                Costs = pickedCostField.CostsL,
                SectorToPicked = locationData.SectorToPicked,
                IntegrationField = path.IntegrationField,
                Target = destinationData.TargetIndex,
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

                Costs = pickedCostField.CostsL,
                SectorToPicked = locationData.SectorToPicked,
                IntegrationField = path.IntegrationField,
                Target = destinationData.TargetIndex,
            };
            losHandle = losjob.Schedule(flowHandle);
            _losCalculatedPaths.Add(pathInfo.PathIndex);
        }
        path.SectorWithinLOSState[0] = SectorsWihinLOSArgument.None;

        if (FlowFieldUtilities.DebugMode) { losHandle.Complete(); }
        pathInfo.Handle = losHandle;
        ScheduledLOS.Add(pathInfo);
    }
    public void TryComplete()
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
    public void ForceComplete()
    {
        for (int i = ScheduledLOS.Length - 1; i >= 0; i--)
        {
            PathPipelineInfoWithHandle losHandle = ScheduledLOS[i];
            losHandle.Handle.Complete();
        }
        ScheduledLOS.Clear();
    }
    public void ScheduleLOSTransfers()
    {
        List<Path> producedPaths = _pathContainer.ProducedPaths;
        NativeList<PathLocationData> pathLocationDataList = _pathContainer.PathLocationDataList;
        NativeList<PathFlowData> pathFlowDataList = _pathContainer.PathFlowDataList;
        NativeList<PathDestinationData> pathDestinationDataList = _pathContainer.PathDestinationDataList;
        for (int i = 0; i < _losCalculatedPaths.Length; i++)
        {
            int pathIndex = _losCalculatedPaths[i];
            Path path = producedPaths[pathIndex];
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
                IntegrationField = path.IntegrationField,
                Target = destinationData.TargetIndex,
            };
            _transferHandles.Add(losTransfer.Schedule());
        }
        _losCalculatedPaths.Clear();
    }
    public void CompleteLOSTransfers()
    {
        for(int i = 0; i < _transferHandles.Length; i++)
        {
            _transferHandles[i].Complete();
        }
        _transferHandles.Clear();
    }
}
