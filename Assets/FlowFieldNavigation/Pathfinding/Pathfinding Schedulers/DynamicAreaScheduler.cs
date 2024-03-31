using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using System.Collections.Generic;

namespace FlowFieldNavigation
{

    internal class DynamicAreaScheduler
    {
        FlowFieldNavigationManager _navigationManager;
        PathDataContainer _pathContainer;

        internal DynamicAreaScheduler(FlowFieldNavigationManager navigationManager)
        {
            _navigationManager = navigationManager;
            _pathContainer = navigationManager.PathDataContainer;
        }
        internal JobHandle ScheduleDynamicArea(NativeArray<int> dynamicAreaRequestPathIndicies)
        {
            NativeArray<JobHandle> tempHandleArray = new NativeArray<JobHandle>(dynamicAreaRequestPathIndicies.Length, Allocator.Temp);
            for(int i = 0; i < dynamicAreaRequestPathIndicies.Length; i++)
            {
                int pathIndex = dynamicAreaRequestPathIndicies[i];
                PathfindingInternalData internalData = _pathContainer.PathfindingInternalDataList[pathIndex];
                PathDestinationData destinationData = _pathContainer.PathDestinationDataList[pathIndex];
                DynamicArea dynamicArea = internalData.DynamicArea;
                NativeList<IntegrationTile> integrationField = dynamicArea.IntegrationField;
                UnsafeList<SectorFlowStart> pickedSectorFlowStarts = dynamicArea.SectorFlowStartCalculationBuffer;
                UnsafeList<FlowData> flowFieldCalculationBuffer = dynamicArea.FlowFieldCalculationBuffer;

                int2 targetIndex = FlowFieldUtilities.PosTo2D(destinationData.Destination, FlowFieldUtilities.TileSize, FlowFieldUtilities.FieldGridStartPosition);
                int2 targetSectorIndex = FlowFieldUtilities.GetSector2D(targetIndex, FlowFieldUtilities.SectorColAmount);
                int2 nsector2d = targetSectorIndex + new int2(0, 1);
                int2 esector2d = targetSectorIndex + new int2(1, 0);
                int2 ssector2d = targetSectorIndex + new int2(0, -1);
                int2 wsector2d = targetSectorIndex + new int2(-1, 0);
                int2 nesector2d = targetSectorIndex + new int2(1, 1);
                int2 sesector2d = targetSectorIndex + new int2(1, -1);
                int2 swsector2d = targetSectorIndex + new int2(-1, -1);
                int2 nwsector2d = targetSectorIndex + new int2(-1, 1);
                int curSector1d = FlowFieldUtilities.To1D(targetSectorIndex, FlowFieldUtilities.SectorMatrixColAmount);
                int nSector1d = FlowFieldUtilities.To1D(nsector2d, FlowFieldUtilities.SectorMatrixColAmount);
                int eSector1d = FlowFieldUtilities.To1D(esector2d, FlowFieldUtilities.SectorMatrixColAmount);
                int sSector1d = FlowFieldUtilities.To1D(ssector2d, FlowFieldUtilities.SectorMatrixColAmount);
                int wSector1d = FlowFieldUtilities.To1D(wsector2d, FlowFieldUtilities.SectorMatrixColAmount);
                int neSector1d = FlowFieldUtilities.To1D(nesector2d, FlowFieldUtilities.SectorMatrixColAmount);
                int seSector1d = FlowFieldUtilities.To1D(sesector2d, FlowFieldUtilities.SectorMatrixColAmount);
                int swSector1d = FlowFieldUtilities.To1D(swsector2d, FlowFieldUtilities.SectorMatrixColAmount);
                int nwSector1d = FlowFieldUtilities.To1D(nwsector2d, FlowFieldUtilities.SectorMatrixColAmount);
                int fieldLength = 1;

                pickedSectorFlowStarts.Clear();
                if (WithinBounds(targetSectorIndex)) { pickedSectorFlowStarts.Add(new SectorFlowStart(curSector1d, fieldLength)); fieldLength += FlowFieldUtilities.SectorTileAmount; }
                if (WithinBounds(nsector2d)) { pickedSectorFlowStarts.Add(new SectorFlowStart(nSector1d, fieldLength)); fieldLength += FlowFieldUtilities.SectorTileAmount; }
                if (WithinBounds(esector2d)) { pickedSectorFlowStarts.Add(new SectorFlowStart(eSector1d, fieldLength)); fieldLength += FlowFieldUtilities.SectorTileAmount; }
                if (WithinBounds(ssector2d)) { pickedSectorFlowStarts.Add(new SectorFlowStart(sSector1d, fieldLength)); fieldLength += FlowFieldUtilities.SectorTileAmount; }
                if (WithinBounds(wsector2d)) { pickedSectorFlowStarts.Add(new SectorFlowStart(wSector1d, fieldLength)); fieldLength += FlowFieldUtilities.SectorTileAmount; }
                if (WithinBounds(nesector2d)) { pickedSectorFlowStarts.Add(new SectorFlowStart(neSector1d, fieldLength)); fieldLength += FlowFieldUtilities.SectorTileAmount; }
                if (WithinBounds(sesector2d)) { pickedSectorFlowStarts.Add(new SectorFlowStart(seSector1d, fieldLength)); fieldLength += FlowFieldUtilities.SectorTileAmount; }
                if (WithinBounds(swsector2d)) { pickedSectorFlowStarts.Add(new SectorFlowStart(swSector1d, fieldLength)); fieldLength += FlowFieldUtilities.SectorTileAmount; }
                if (WithinBounds(nwsector2d)) { pickedSectorFlowStarts.Add(new SectorFlowStart(nwSector1d, fieldLength)); fieldLength += FlowFieldUtilities.SectorTileAmount; }

                integrationField.Resize(fieldLength, NativeArrayOptions.UninitializedMemory);
                flowFieldCalculationBuffer.Resize(fieldLength, NativeArrayOptions.ClearMemory);
                integrationField.Length = fieldLength;
                flowFieldCalculationBuffer.Length = fieldLength;

                dynamicArea = new DynamicArea()
                {
                    IntegrationField = integrationField,
                    FlowFieldCalculationBuffer = flowFieldCalculationBuffer,
                    SectorFlowStartCalculationBuffer = pickedSectorFlowStarts,
                };
                internalData.DynamicArea = dynamicArea;
                _pathContainer.PathfindingInternalDataList[pathIndex] = internalData;


                DynamicAreaIntegrationJob integration = new DynamicAreaIntegrationJob()
                {
                    SectorColAmount = FlowFieldUtilities.SectorColAmount,
                    SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
                    SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
                    FieldColAmount = FlowFieldUtilities.FieldColAmount,
                    TargetIndex = targetIndex,
                    Costs = _navigationManager.FieldDataContainer.GetCostFieldWithOffset(destinationData.Offset).Costs,
                    PickedSectorFlowStarts = pickedSectorFlowStarts,
                    IntegrationField = integrationField.AsArray(),
                };
                JobHandle integrationHandle = integration.Schedule();

                DynamicAreaFlowFieldJob flowJob = new DynamicAreaFlowFieldJob()
                {
                    SectorColAmount = FlowFieldUtilities.SectorColAmount,
                    SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
                    SectorMatrixRowAmount = FlowFieldUtilities.SectorMatrixColAmount,
                    SectorMatrixTileAmount = FlowFieldUtilities.SectorMatrixTileAmount,
                    SectorRowAmount = FlowFieldUtilities.SectorMatrixRowAmount,
                    SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
                    TileSize = FlowFieldUtilities.TileSize,
                    FieldColAmount = FlowFieldUtilities.FieldColAmount,
                    FieldTileAmount = FlowFieldUtilities.FieldTileAmount,
                    FieldGridStartPos = FlowFieldUtilities.FieldGridStartPosition,
                    CombinedDynamicAreaFieldReader = new CombinedDynamicAreaFieldReader()
                    {
                        SectorFlowStartIndicies = pickedSectorFlowStarts,
                        FlowField = flowFieldCalculationBuffer,
                        IntegrationField = integrationField.AsArray(),
                    },
                    Costs = _navigationManager.FieldDataContainer.GetCostFieldWithOffset(destinationData.Offset).Costs,
                };

                JobHandle flowHandle = flowJob.Schedule(flowFieldCalculationBuffer.Length, 64, integrationHandle);
                if (FlowFieldUtilities.DebugMode) { { flowHandle.Complete(); } }
                tempHandleArray[i] = flowHandle;
            }
            return JobHandle.CombineDependencies(tempHandleArray);

            bool WithinBounds(int2 sectorIndex)
            {
                return sectorIndex.x < FlowFieldUtilities.SectorMatrixColAmount && sectorIndex.x >= 0 && sectorIndex.y < FlowFieldUtilities.SectorMatrixRowAmount && sectorIndex.y >= 0;
            }
        }
    }
}