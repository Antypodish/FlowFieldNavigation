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
        NativeList<FlowFieldCalculationBufferParent> _flowFieldCalculationBuffers;
        NativeList<JobHandle> _flowTransferHandles;
        NativeList<int> _flowFieldResizedPaths;
        internal FlowCalculationScheduler(FlowFieldNavigationManager navigationManager, LOSIntegrationScheduler losIntegrationScheduler)
        {
            _navigationManager = navigationManager;
            _pathContainer = navigationManager.PathDataContainer;
            _flowFieldCalculationBuffers = new NativeList<FlowFieldCalculationBufferParent>(Allocator.Persistent);
            _flowTransferHandles = new NativeList<JobHandle>(Allocator.Persistent);
            _losIntegrationScheduler = losIntegrationScheduler;
            _flowFieldResizedPaths = new NativeList<int>(Allocator.Persistent);
        }
        internal void DisposeAll()
        {
            if (_flowFieldCalculationBuffers.IsCreated)
            {
                for (int i = 0; i < _flowFieldCalculationBuffers.Length; i++)
                {
                    FlowFieldCalculationBufferParent bufferParent = _flowFieldCalculationBuffers[i];
                    bufferParent.BufferParent.Dispose();
                }
                _flowFieldCalculationBuffers.Dispose();
            }
            if (_flowTransferHandles.IsCreated) { _flowTransferHandles.Dispose(); }
            if (_flowFieldResizedPaths.IsCreated) { _flowFieldResizedPaths.Dispose(); }
            _losIntegrationScheduler.DisposeAll();
            _losIntegrationScheduler = null;
        }
        internal JobHandle ScheduleFlow(int pathIndex)
        {
            PathfindingInternalData pathInternalData = _pathContainer.PathfindingInternalDataList[pathIndex];
            PathLocationData locationData = _pathContainer.PathLocationDataList[pathIndex];
            PathDestinationData destinationData = _pathContainer.PathDestinationDataList[pathIndex];
            CostField pickedCostField = _navigationManager.FieldDataContainer.GetCostFieldWithOffset(destinationData.Offset);
            int2 targetIndex = FlowFieldUtilities.PosTo2D(destinationData.Destination, FlowFieldUtilities.TileSize, FlowFieldUtilities.FieldGridStartPosition);

            //RESET NEW INT FIELD INDICIES
            int lastIntegrationFieldLength = pathInternalData.IntegrationField.Length;
            int curIntegrationFieldLength = pathInternalData.FlowFieldLength.Value;
            if (lastIntegrationFieldLength != curIntegrationFieldLength)
            {
                pathInternalData.IntegrationField.Length = curIntegrationFieldLength;
                _flowFieldResizedPaths.Add(pathIndex);
            }

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

            //SCHEDULE FLOW FIELDS
            NativeList<JobHandle> flowfieldHandles = new NativeList<JobHandle>(Allocator.Temp);
            UnsafeList<FlowFieldCalculationBuffer> bufferParent = new UnsafeList<FlowFieldCalculationBuffer>(sectorIndiciesToCalculateFlow.Length, Allocator.Persistent);
            bufferParent.Length = sectorIndiciesToCalculateFlow.Length;

            for (int i = 0; i < sectorIndiciesToCalculateFlow.Length; i++)
            {
                int sectorIndex = sectorIndiciesToCalculateFlow[i];
                int sectorStart = locationData.SectorToPicked[sectorIndex];

                UnsafeList<FlowData> flowFieldCalculationBuffer = new UnsafeList<FlowData>(FlowFieldUtilities.SectorTileAmount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                flowFieldCalculationBuffer.Length = FlowFieldUtilities.SectorTileAmount;
                FlowFieldJob ffJob = new FlowFieldJob()
                {
                    TileSize = FlowFieldUtilities.TileSize,
                    SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
                    SectorColAmount = FlowFieldUtilities.SectorColAmount,
                    SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
                    SectorMatrixRowAmount = FlowFieldUtilities.SectorMatrixRowAmount,
                    SectorMatrixTileAmount = FlowFieldUtilities.SectorMatrixTileAmount,
                    SectorStartIndex = sectorStart,
                    FieldTileAmount = FlowFieldUtilities.FieldTileAmount,
                    FieldColAmount = FlowFieldUtilities.FieldColAmount,
                    SectorRowAmount = FlowFieldUtilities.SectorRowAmount,
                    FieldGridStartPos = FlowFieldUtilities.FieldGridStartPosition,
                    SectorToPicked = locationData.SectorToPicked,
                    PickedToSector = pathInternalData.PickedSectorList.AsArray(),
                    FlowFieldCalculationBuffer = flowFieldCalculationBuffer,
                    IntegrationField = pathInternalData.IntegrationField.AsArray(),
                    Costs = pickedCostField.Costs,
                };
                JobHandle flowHandle = ffJob.Schedule(flowFieldCalculationBuffer.Length, 256, integrationHandle);
                flowfieldHandles.Add(flowHandle);

                //PUT BUFFER PARENT TO THE INDEX
                bufferParent[i] = new FlowFieldCalculationBuffer()
                {
                    FlowFieldStartIndex = sectorStart,
                    Buffer = flowFieldCalculationBuffer,
                };
            }

            //PUSH BUFFER PARENT TO THE LIST
            FlowFieldCalculationBufferParent parent = new FlowFieldCalculationBufferParent()
            {
                PathIndex = pathIndex,
                BufferParent = bufferParent,
            };
            _flowFieldCalculationBuffers.Add(parent);

            JobHandle flowFieldCombinedHandle = JobHandle.CombineDependencies(flowfieldHandles.AsArray());

            if (FlowFieldUtilities.DebugMode) { flowFieldCombinedHandle.Complete(); }
            return flowFieldCombinedHandle;
        }
        internal void TryComplete()
        {/*
            for (int i = ScheduledFlow.Length - 1; i >= 0; i--)
            {
                PathPipelineInfoWithHandle flowHandle = ScheduledFlow[i];
                if (flowHandle.Handle.IsCompleted)
                {
                    flowHandle.Handle.Complete();
                    ScheduledFlow.RemoveAtSwapBack(i);
                }
            }
            _losIntegrationScheduler.TryComplete();*/
        }
        internal void ForceComplete()
        {/*
            for (int i = ScheduledFlow.Length - 1; i >= 0; i--)
            {
                PathPipelineInfoWithHandle flowHandle = ScheduledFlow[i];
                flowHandle.Handle.Complete();
            }
            ScheduledFlow.Clear();*/
            RefreshResizedFlowFieldLengths();
            _losIntegrationScheduler.ScheduleLOSTransfers();
            ScheduleFlowTransfers();
            _losIntegrationScheduler.CompleteLOSTransfers();
            CompleteFlowTransfers();
            DisposeFlowFieldCalculationBuffers();
        }
        void ScheduleFlowTransfers()
        {
            NativeList<PathFlowData> flowDataList = _pathContainer.PathFlowDataList;
            for (int i = 0; i < _flowFieldCalculationBuffers.Length; i++)
            {
                FlowFieldCalculationBufferParent parent = _flowFieldCalculationBuffers[i];
                int pathIndex = parent.PathIndex;

                FlowFieldCalculationTransferJob transferJob = new FlowFieldCalculationTransferJob()
                {
                    CalculationBufferParent = parent,
                    FlowField = flowDataList[pathIndex].FlowField,
                };
                _flowTransferHandles.Add(transferJob.Schedule());
            }
        }
        void CompleteFlowTransfers()
        {
            for (int i = 0; i < _flowTransferHandles.Length; i++)
            {
                _flowTransferHandles[i].Complete();
            }
        }
        void DisposeFlowFieldCalculationBuffers()
        {
            for (int i = 0; i < _flowFieldCalculationBuffers.Length; i++)
            {
                FlowFieldCalculationBufferParent calculationBufferParent = _flowFieldCalculationBuffers[i];
                UnsafeList<FlowFieldCalculationBuffer> bufferParent = calculationBufferParent.BufferParent;
                for (int j = 0; j < bufferParent.Length; j++)
                {
                    UnsafeList<FlowData> buffer = bufferParent[j].Buffer;
                    buffer.Dispose();
                }
                bufferParent.Dispose();
            }
            _flowFieldCalculationBuffers.Clear();
        }
        void RefreshResizedFlowFieldLengths()
        {
            List<PathfindingInternalData> pathfindingInternalDataList = _pathContainer.PathfindingInternalDataList;
            NativeList<PathFlowData> flowDataList = _pathContainer.PathFlowDataList;
            for (int i = 0; i < _flowFieldResizedPaths.Length; i++)
            {
                int pathIndex = _flowFieldResizedPaths[i];
                PathfindingInternalData pathInternalData = pathfindingInternalDataList[pathIndex];
                PathFlowData flowData = flowDataList[pathIndex];

                UnsafeList<FlowData> flowfield = flowData.FlowField;
                flowfield.Resize(pathInternalData.FlowFieldLength.Value, NativeArrayOptions.ClearMemory);
                flowData.FlowField = flowfield;

                UnsafeLOSBitmap losmap = flowData.LOSMap;
                losmap.Resize(pathInternalData.FlowFieldLength.Value, NativeArrayOptions.ClearMemory);
                flowData.LOSMap = losmap;

                flowDataList[pathIndex] = flowData;
            }
            _flowFieldResizedPaths.Clear();
        }
    }


}