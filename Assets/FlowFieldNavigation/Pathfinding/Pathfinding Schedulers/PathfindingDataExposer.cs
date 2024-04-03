using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Jobs;

namespace FlowFieldNavigation
{
    internal class PathfindingDataExposer
    {
        PathDataContainer _pathContainer;

        internal PathfindingDataExposer(PathDataContainer pathDataContainer)
        {
            _pathContainer = pathDataContainer;
        }
        internal void Expose(
            NativeArray<int> pathIndiciesOfScheduledDynamicAreas,
            NativeArray<PortalTraversalRequest> portalTraversalRequestedPaths,
            NativeArray<int> _losCalculatedPaths,
            NativeArray<FlowRequest> flowRequests, 
            NativeArray<int> destinationUpdatedPathIndicies, 
            NativeArray<int> newPathIndicies, 
            NativeArray<int> expandedPathIndicies
            )
        {
            ExposeDynamicArea(pathIndiciesOfScheduledDynamicAreas);
            RefreshResizedFlowFieldLengths(portalTraversalRequestedPaths);
            ScheduleLOSTransfers(_losCalculatedPaths);
            ScheduleFlowTransfers(flowRequests);
            _pathContainer.ExposeBuffers(destinationUpdatedPathIndicies, newPathIndicies, expandedPathIndicies);
        }
        void ScheduleFlowTransfers(NativeArray<FlowRequest> flowRequests)
        {
            int sectorTileAmount = FlowFieldUtilities.SectorTileAmount;
            PathSectorToFlowStartMapper newFlowStartMap = _pathContainer.SectorFlowStartMap;
            NativeList<FlowData> exposedFlowData = _pathContainer.ExposedFlowData;
            for (int i = 0; i < flowRequests.Length; i++)
            {
                FlowRequest req = flowRequests[i];
                PathfindingInternalData pathInternalData = _pathContainer.PathfindingInternalDataList[req.PathIndex];
                NativeArray<int> flowCalculatedSectorIndicies = pathInternalData.SectorIndiciesToCalculateFlow.AsArray();
                NativeArray<int> sectorToFlowStartTable = _pathContainer.SectorToFlowStartTables[req.PathIndex];
                NativeArray<FlowData> calculationBuffer = pathInternalData.FlowFieldCalculationBuffer.AsArray();
                for (int j = 0; j < flowCalculatedSectorIndicies.Length; j++)
                {
                    int sectorIndex = flowCalculatedSectorIndicies[j];
                    newFlowStartMap.TryGet(req.PathIndex, sectorIndex, out int newSectorFlowStartIndex);
                    NativeSlice<FlowData> fromSlice = new NativeSlice<FlowData>(calculationBuffer, j * sectorTileAmount, sectorTileAmount);
                    Transfer(fromSlice, exposedFlowData.AsArray(), newSectorFlowStartIndex);
                }
            }

            void Transfer(NativeSlice<FlowData> fromSlice, NativeArray<FlowData> toList, int listStartIndex)
            {
                for (int i = 0; i < fromSlice.Length; i++)
                {
                    toList[listStartIndex + i] = fromSlice[i];
                }
            }
        }
        internal void ScheduleLOSTransfers(NativeArray<int> _losCalculatedPaths)
        {
            List<PathfindingInternalData> internalDataList = _pathContainer.PathfindingInternalDataList;
            NativeList<PathDestinationData> pathDestinationDataList = _pathContainer.PathDestinationDataList;
            PathSectorToFlowStartMapper newFlowStartMap = _pathContainer.SectorFlowStartMap;
            NativeArray<bool> exposedLosData = _pathContainer.ExposedLosData.AsArray();
            for (int i = 0; i < _losCalculatedPaths.Length; i++)
            {
                int pathIndex = _losCalculatedPaths[i];
                PathfindingInternalData internalData = internalDataList[pathIndex];
                PathDestinationData destinationData = pathDestinationDataList[pathIndex];
                NativeArray<int> sectorToFlowStartTable = _pathContainer.SectorToFlowStartTables[pathIndex];
                LOSTransferJob losTransfer = new LOSTransferJob()
                {
                    SectorColAmount = FlowFieldUtilities.SectorColAmount,
                    SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
                    SectorMatrixRowAmount = FlowFieldUtilities.SectorMatrixRowAmount,
                    SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
                    LOSRange = FlowFieldUtilities.LOSRange,
                    PathIndex = pathIndex,
                    SectorFlowStartTable = sectorToFlowStartTable,
                    FlowStartMap = newFlowStartMap,
                    LosArray = exposedLosData,
                    IntegrationField = internalData.IntegrationField.AsArray(),
                    Target = FlowFieldUtilities.PosTo2D(destinationData.Destination, FlowFieldUtilities.TileSize, FlowFieldUtilities.FieldGridStartPosition),
                };
                losTransfer.Schedule().Complete();
            }
        }
        void RefreshResizedFlowFieldLengths(NativeArray<PortalTraversalRequest> portalTraversalRequestedPaths)
        {
            List<PathfindingInternalData> pathfindingInternalDataList = _pathContainer.PathfindingInternalDataList;
            List<PathPortalTraversalData> pathPorTravDataList = _pathContainer.PathPortalTraversalDataList;
            PathSectorToFlowStartMapper newFlowStartMap = _pathContainer.SectorFlowStartMap;
            NativeList<FlowData> exposedFlowData = _pathContainer.ExposedFlowData;
            NativeList<bool> exposedLosData = _pathContainer.ExposedLosData;
            for (int i = 0; i < portalTraversalRequestedPaths.Length; i++)
            {
                PortalTraversalRequest request = portalTraversalRequestedPaths[i];
                int pathIndex = request.PathIndex;
                NativeArray<int> allPickedSectors = pathfindingInternalDataList[pathIndex].PickedSectorList.AsArray();
                int newPickedSectorStartIndex = pathPorTravDataList[pathIndex].NewPickedSectorStartIndex.Value;
                for(int j = newPickedSectorStartIndex; j < allPickedSectors.Length; j++)
                {
                    if(newFlowStartMap.TryAdd(pathIndex, allPickedSectors[j], exposedFlowData.Length))
                    {
                        exposedFlowData.Length += FlowFieldUtilities.SectorTileAmount;
                        exposedLosData.Length += FlowFieldUtilities.SectorTileAmount;
                    }
                }
            }
        }
        void ExposeDynamicArea(NativeArray<int> pathIndiciesOfScheduledDynamicAreas)
        {
            List<PathfindingInternalData> pathfindingInternalDataList = _pathContainer.PathfindingInternalDataList;
            NativeList<PathLocationData> pathLocationDataList = _pathContainer.PathLocationDataList;
            NativeList<PathFlowData> pathFlowDataList = _pathContainer.PathFlowDataList;
            NativeList<JobHandle> handles = new NativeList<JobHandle>(Allocator.Temp);
            for (int i = 0; i < pathIndiciesOfScheduledDynamicAreas.Length; i++)
            {
                int pathIndex = pathIndiciesOfScheduledDynamicAreas[i];
                PathfindingInternalData pathInternalData = pathfindingInternalDataList[pathIndex];
                PathFlowData pathFlowData = pathFlowDataList[pathIndex];
                PathLocationData pathLocationData = pathLocationDataList[pathIndex];

                //COPY FLOW FIELD
                UnsafeList<FlowData> flowField = pathFlowData.DynamicAreaFlowField;
                UnsafeList<FlowData> flowCalculationBuffer = pathInternalData.DynamicArea.FlowFieldCalculationBuffer;
                flowField.Resize(flowCalculationBuffer.Length, NativeArrayOptions.UninitializedMemory);
                flowField.Length = flowCalculationBuffer.Length;
                UnsafeListCopyJob<FlowData> copyJob = new UnsafeListCopyJob<FlowData>()
                {
                    Destination = flowField,
                    Source = flowCalculationBuffer,
                };
                handles.Add(copyJob.Schedule());

                //COPY SECTOR FLOW STARTS
                UnsafeList<SectorFlowStart> sectorFlowStarts = pathLocationData.DynamicAreaPickedSectorFlowStarts;
                UnsafeList<SectorFlowStart> sectorFlowStartCalculationBuffer = pathInternalData.DynamicArea.SectorFlowStartCalculationBuffer;
                sectorFlowStarts.Length = sectorFlowStartCalculationBuffer.Length;
                sectorFlowStarts.CopyFrom(sectorFlowStartCalculationBuffer);

                //SEND DATA BACK
                pathLocationData.DynamicAreaPickedSectorFlowStarts = sectorFlowStarts;
                pathLocationDataList[pathIndex] = pathLocationData;
                pathFlowData.DynamicAreaFlowField = flowField;
                pathFlowDataList[pathIndex] = pathFlowData;
            }
            JobHandle.CompleteAll(handles.AsArray());

        }
    }
}