using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace FlowFieldNavigation
{
    internal class LOSIntegrationScheduler
    {
        FlowFieldNavigationManager _navigationManager;
        PathDataContainer _pathContainer;

        NativeList<int> _losCalculatedPaths;
        NativeList<JobHandle> _transferHandles;

        internal LOSIntegrationScheduler(FlowFieldNavigationManager navigationManager)
        {
            _navigationManager = navigationManager;
            _pathContainer = navigationManager.PathDataContainer;
            _losCalculatedPaths = new NativeList<int>(Allocator.Persistent);
            _transferHandles = new NativeList<JobHandle>(Allocator.Persistent);
        }
        internal void DisposeAll()
        {
            if (_losCalculatedPaths.IsCreated) { _losCalculatedPaths.Dispose(); }
            if (_transferHandles.IsCreated) { _transferHandles.Dispose(); }
        }

        internal JobHandle ScheduleLOS(NativeArray<LosRequest> losRequestsUnique, JobHandle dependency)
        {
            NativeArray<JobHandle> tempHandleArray = new NativeArray<JobHandle>(losRequestsUnique.Length, Allocator.Temp);
            for(int i = 0; i < losRequestsUnique.Length; i++)
            {
                LosRequest req = losRequestsUnique[i];
                int pathIndex = req.PathIndex;
                DynamicDestinationState destinationState = req.DestinationState;
                PathfindingInternalData internalData = _pathContainer.PathfindingInternalDataList[pathIndex];
                PathDestinationData destinationData = _pathContainer.PathDestinationDataList[pathIndex];
                PathLocationData locationData = _pathContainer.PathLocationDataList[pathIndex];
                int2 targetIndex = FlowFieldUtilities.PosTo2D(destinationData.Destination, FlowFieldUtilities.TileSize, FlowFieldUtilities.FieldGridStartPosition);
                CostField pickedCostField = _navigationManager.FieldDataContainer.GetCostFieldWithOffset(destinationData.Offset);

                JobHandle losHandle = dependency;
                bool requestedSectorWithinLOS = (internalData.SectorWithinLOSState.Value & SectorsWihinLOSArgument.RequestedSectorWithinLOS) == SectorsWihinLOSArgument.RequestedSectorWithinLOS;
                bool addedSectorWithinLOS = (internalData.SectorWithinLOSState.Value & SectorsWihinLOSArgument.AddedSectorWithinLOS) == SectorsWihinLOSArgument.AddedSectorWithinLOS;
                bool losCalculated = internalData.LOSCalculatedFlag.Value;
                bool destinationMoved = destinationState == DynamicDestinationState.Moved;
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
                    JobHandle loscleanHandle = losClean.Schedule(dependency);

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
                    _losCalculatedPaths.Add(pathIndex);
                }
                else if (!losCalculated && requestedSectorWithinLOS)
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
                    losHandle = losjob.Schedule(dependency);
                    _losCalculatedPaths.Add(pathIndex);
                    internalData.LOSCalculatedFlag.Value = true;
                }
                internalData.SectorWithinLOSState.Value = SectorsWihinLOSArgument.None;
                if (FlowFieldUtilities.DebugMode) { losHandle.Complete(); }
                tempHandleArray[i] = losHandle;
            }
            return JobHandle.CombineDependencies(tempHandleArray);
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
            for (int i = 0; i < _transferHandles.Length; i++)
            {
                _transferHandles[i].Complete();
            }
            _transferHandles.Clear();
        }
    }


}