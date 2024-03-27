using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using System.Diagnostics;

namespace FlowFieldNavigation
{
    internal class FlowCalculationScheduler
    {
        FlowFieldNavigationManager _navigationManager;
        PathDataContainer _pathContainer;
        LOSIntegrationScheduler _losIntegrationScheduler;

        internal FlowCalculationScheduler(FlowFieldNavigationManager navigationManager, LOSIntegrationScheduler losIntegrationScheduler)
        {
            _navigationManager = navigationManager;
            _pathContainer = navigationManager.PathDataContainer;
            _losIntegrationScheduler = losIntegrationScheduler;
        }
        internal void DisposeAll()
        {
            _losIntegrationScheduler.DisposeAll();
            _losIntegrationScheduler = null;
        }
        internal JobHandle ScheduleFlow(NativeArray<FlowRequest> flowRequestsUnique)
        {
            NativeArray<JobHandle> tempHandleArray = new NativeArray<JobHandle>(flowRequestsUnique.Length, Allocator.Temp);
            for(int i = 0; i < flowRequestsUnique.Length; i++)
            {
                FlowRequest req = flowRequestsUnique[i];
                int pathIndex = req.PathIndex;
                PathfindingInternalData pathInternalData = _pathContainer.PathfindingInternalDataList[pathIndex];
                PathLocationData locationData = _pathContainer.PathLocationDataList[pathIndex];
                PathDestinationData destinationData = _pathContainer.PathDestinationDataList[pathIndex];
                CostField pickedCostField = _navigationManager.FieldDataContainer.GetCostFieldWithOffset(destinationData.Offset);
                int2 targetIndex = FlowFieldUtilities.PosTo2D(destinationData.Destination, FlowFieldUtilities.TileSize, FlowFieldUtilities.FieldGridStartPosition);

                //SCHEDULE INTEGRATION FIELDS
                NativeArray<int> sectorIndiciesToCalculateIntegration = pathInternalData.SectorIndiciesToCalculateIntegration.AsArray();
                NativeArray<int> sectorIndiciesToCalculateFlow = pathInternalData.SectorIndiciesToCalculateFlow.AsArray();

                IntegrationFieldJob integrationJob = new IntegrationFieldJob()
                {
                    TargetIndex = targetIndex,
                    SectorColAmount = FlowFieldUtilities.SectorColAmount,
                    SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
                    SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
                    FieldColAmount = FlowFieldUtilities.FieldColAmount,
                    FieldRowAmount = FlowFieldUtilities.FieldRowAmount,
                    SectorFlowStartTable = locationData.SectorToPicked,
                    SectorIndiciesToCalculateIntegration = sectorIndiciesToCalculateIntegration,
                    CostField = pickedCostField.Costs,
                    IntegrationField = pathInternalData.IntegrationField.AsArray(),
                    SectorToWaveFrontsMap = pathInternalData.SectorToWaveFrontsMap,
                };
                JobHandle integrationHandle = integrationJob.Schedule();

                FlowFieldJob flowFieldJob = new FlowFieldJob()
                {
                    SectorColAmount = FlowFieldUtilities.SectorColAmount,
                    SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
                    SectorMatrixRowAmount = FlowFieldUtilities.SectorMatrixRowAmount,
                    SectorMatrixTileAmount = FlowFieldUtilities.SectorMatrixTileAmount,
                    SectorRowAmount = FlowFieldUtilities.SectorRowAmount,
                    SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
                    TileSize = FlowFieldUtilities.TileSize,
                    FieldGridStartPos = FlowFieldUtilities.FieldGridStartPosition,
                    FieldColAmount = FlowFieldUtilities.FieldColAmount,
                    FieldTileAmount = FlowFieldUtilities.FieldTileAmount,
                    SectorIndiciesToCalculateFlow = sectorIndiciesToCalculateFlow,
                    SectorToPicked = locationData.SectorToPicked,
                    PickedToSector = pathInternalData.PickedSectorList.AsArray(),
                    Costs = pickedCostField.Costs,
                    IntegrationField = pathInternalData.IntegrationField.AsArray(),
                    FlowFieldCalculationBuffer = pathInternalData.FlowFieldCalculationBuffer,
                };
                JobHandle flowFieldHandle = flowFieldJob.Schedule(integrationHandle);

                if (FlowFieldUtilities.DebugMode) { flowFieldHandle.Complete(); }
                tempHandleArray[i] = flowFieldHandle;
            }
            return JobHandle.CombineDependencies(tempHandleArray);
        }
        internal void ForceComplete(NativeArray<FlowRequest> flowRequests, NativeArray<PortalTraversalRequest> portalTraversalRequests)
        {
            RefreshResizedFlowFieldLengths(portalTraversalRequests);
            _losIntegrationScheduler.ScheduleLOSTransfers();
            ScheduleFlowTransfers(flowRequests);
            _losIntegrationScheduler.CompleteLOSTransfers();
        }
        void ScheduleFlowTransfers(NativeArray<FlowRequest> flowRequests)
        {
            int sectorTileAmount = FlowFieldUtilities.SectorTileAmount;
            for(int i = 0; i< flowRequests.Length; i++)
            {
                FlowRequest req = flowRequests[i];
                PathfindingInternalData pathInternalData = _pathContainer.PathfindingInternalDataList[req.PathIndex];
                PathLocationData pathLocationData = _pathContainer.PathLocationDataList[req.PathIndex];
                PathFlowData pathFlowData = _pathContainer.PathFlowDataList[req.PathIndex];
                NativeArray<int> flowCalculatedSectorIndicies = pathInternalData.SectorIndiciesToCalculateFlow.AsArray();
                UnsafeList<int> sectorToPicked = pathLocationData.SectorToPicked;
                NativeArray<FlowData> calculationBuffer = pathInternalData.FlowFieldCalculationBuffer.AsArray();
                for(int j = 0; j < flowCalculatedSectorIndicies.Length; j++)
                {
                    int sectorIndex = flowCalculatedSectorIndicies[j];
                    int sectorFlowStartIndex = sectorToPicked[sectorIndex];
                    NativeSlice<FlowData> fromSlice = new NativeSlice<FlowData>(calculationBuffer, j * sectorTileAmount, sectorTileAmount);
                    Transfer(fromSlice, pathFlowData.FlowField, sectorFlowStartIndex);
                }
            }

            void Transfer(NativeSlice<FlowData> fromSlice, UnsafeList<FlowData> toList, int listStartIndex)
            {
                for(int i = 0; i < fromSlice.Length; i++)
                {
                    toList[listStartIndex + i] = fromSlice[i];
                }
            }
        }
        void RefreshResizedFlowFieldLengths(NativeArray<PortalTraversalRequest> portalTraversalRequestedPaths)
        {
            List<PathfindingInternalData> pathfindingInternalDataList = _pathContainer.PathfindingInternalDataList;
            NativeList<PathFlowData> flowDataList = _pathContainer.PathFlowDataList;
            for (int i = 0; i < portalTraversalRequestedPaths.Length; i++)
            {
                PortalTraversalRequest request = portalTraversalRequestedPaths[i];
                int pathIndex = request.PathIndex;
                PathfindingInternalData pathInternalData = pathfindingInternalDataList[pathIndex];
                PathFlowData flowData = flowDataList[pathIndex];

                UnsafeList<FlowData> flowfield = flowData.FlowField;
                flowfield.Resize(pathInternalData.IntegrationField.Length, NativeArrayOptions.ClearMemory);
                flowData.FlowField = flowfield;

                UnsafeLOSBitmap losmap = flowData.LOSMap;
                losmap.Resize(pathInternalData.IntegrationField.Length, NativeArrayOptions.ClearMemory);
                flowData.LOSMap = losmap;

                flowDataList[pathIndex] = flowData;
            }
        }
    }


}