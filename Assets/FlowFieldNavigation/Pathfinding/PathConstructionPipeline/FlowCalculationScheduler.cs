using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

public class FlowCalculationScheduler
{
    PathfindingManager _pathfindingManager;
    PathDataContainer _pathContainer;
    LOSIntegrationScheduler _losIntegrationScheduler;
    NativeList<PathPipelineInfoWithHandle> ScheduledFlow;
    NativeList<FlowFieldCalculationBufferParent> _flowFieldCalculationBuffers;
    NativeList<JobHandle> _flowTransferHandles;
    NativeList<int> _flowFieldResizedPaths;
    public FlowCalculationScheduler(PathfindingManager pathfindingManager, LOSIntegrationScheduler losIntegrationScheduler)
    {
        _pathfindingManager = pathfindingManager;
        _pathContainer = pathfindingManager.PathContainer;
        ScheduledFlow = new NativeList<PathPipelineInfoWithHandle>(Allocator.Persistent);
        _flowFieldCalculationBuffers = new NativeList<FlowFieldCalculationBufferParent>(Allocator.Persistent);
        _flowTransferHandles = new NativeList<JobHandle>(Allocator.Persistent);
        _losIntegrationScheduler = losIntegrationScheduler;
        _flowFieldResizedPaths = new NativeList<int>(Allocator.Persistent);
    }

    public void ScheduleFlow(PathPipelineInfoWithHandle pathInfo)
    {
        PathfindingInternalData pathInternalData = _pathContainer.PathfindingInternalDataList[pathInfo.PathIndex];
        PathLocationData locationData = _pathContainer.PathLocationDataList[pathInfo.PathIndex];
        PathDestinationData destinationData = _pathContainer.PathDestinationDataList[pathInfo.PathIndex];
        CostField pickedCostField = _pathfindingManager.FieldManager.GetCostFieldWithOffset(destinationData.Offset);

        //RESET NEW INT FIELD INDICIES
        int lastIntegrationFieldLength = pathInternalData.IntegrationField.Length;
        int curIntegrationFieldLength = pathInternalData.FlowFieldLength[0];
        if (lastIntegrationFieldLength != curIntegrationFieldLength)
        {
            pathInternalData.IntegrationField.Resize(curIntegrationFieldLength, NativeArrayOptions.UninitializedMemory);
            //RESET NEW INT FIELD INDICIES
            IntegrationFieldResetJob resJob = new IntegrationFieldResetJob()
            {
                StartIndex = lastIntegrationFieldLength,
                IntegrationField = pathInternalData.IntegrationField,
            };
            resJob.Schedule().Complete();
            _flowFieldResizedPaths.Add(pathInfo.PathIndex);
        }

        //SCHEDULE INTEGRATION FIELDS
        NativeList<JobHandle> intFieldHandles = new NativeList<JobHandle>(Allocator.Temp);
        NativeArray<int> sectorFlowStartIndiciesToCalculateIntegration = pathInternalData.SectorFlowStartIndiciesToCalculateIntegration;
        NativeArray<int> sectorFlowStartIndiciesToCalculateFlow = pathInternalData.SectorFlowStartIndiciesToCalculateFlow;
        for (int i = 0; i < sectorFlowStartIndiciesToCalculateIntegration.Length; i++)
        {
            int sectorStart = sectorFlowStartIndiciesToCalculateIntegration[i];
            int sectorIndex = pathInternalData.PickedSectorList[(sectorStart - 1) / FlowFieldUtilities.SectorTileAmount];
            NativeSlice<IntegrationTile> integrationSector = new NativeSlice<IntegrationTile>(pathInternalData.IntegrationField, sectorStart, FlowFieldUtilities.SectorTileAmount);
            IntegrationFieldJob intJob = new IntegrationFieldJob()
            {
                SectorIndex = sectorIndex,
                StartIndicies = pathInternalData.ActivePortalList[(sectorStart - 1) / FlowFieldUtilities.SectorTileAmount],
                Costs = new NativeSlice<byte>(pickedCostField.Costs, sectorIndex * FlowFieldUtilities.SectorTileAmount, FlowFieldUtilities.SectorTileAmount),
                IntegrationField = integrationSector,
                SectorToPicked = locationData.SectorToPicked,
                SectorColAmount = FlowFieldUtilities.SectorColAmount,
                SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
                FieldColAmount = FlowFieldUtilities.FieldColAmount,
                FieldRowAmount = FlowFieldUtilities.FieldRowAmount,
            };
            JobHandle intHandle = intJob.Schedule();
            intFieldHandles.Add(intHandle);
        }
        JobHandle intFieldCombinedHandle = JobHandle.CombineDependencies(intFieldHandles);

        //SCHEDULE FLOW FIELDS
        NativeList<JobHandle> flowfieldHandles = new NativeList<JobHandle>(Allocator.Temp);
        UnsafeList<FlowFieldCalculationBuffer> bufferParent = new UnsafeList<FlowFieldCalculationBuffer>(sectorFlowStartIndiciesToCalculateFlow.Length, Allocator.Persistent);
        bufferParent.Length = sectorFlowStartIndiciesToCalculateFlow.Length;

        for (int i = 0; i < sectorFlowStartIndiciesToCalculateFlow.Length; i++)
        {
            int sectorStart = sectorFlowStartIndiciesToCalculateFlow[i];

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
                SectorToPicked = locationData.SectorToPicked,
                PickedToSector = pathInternalData.PickedSectorList,
                FlowFieldCalculationBuffer = flowFieldCalculationBuffer,
                IntegrationField = pathInternalData.IntegrationField,
                Costs = pickedCostField.Costs,
            };
            JobHandle flowHandle = ffJob.Schedule(flowFieldCalculationBuffer.Length, 256, intFieldCombinedHandle);
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
            PathIndex = pathInfo.PathIndex,
            BufferParent = bufferParent,
        };
        _flowFieldCalculationBuffers.Add(parent);

        JobHandle flowFieldCombinedHandle = JobHandle.CombineDependencies(flowfieldHandles);
        _losIntegrationScheduler.ScheduleLOS(pathInfo, flowFieldCombinedHandle);

        if (FlowFieldUtilities.DebugMode) { flowFieldCombinedHandle.Complete(); }
        pathInfo.Handle = flowFieldCombinedHandle;
        ScheduledFlow.Add(pathInfo);
    }
    public void TryComplete()
    {
        for (int i = ScheduledFlow.Length - 1; i >= 0; i--)
        {
            PathPipelineInfoWithHandle flowHandle = ScheduledFlow[i];
            if (flowHandle.Handle.IsCompleted)
            {
                flowHandle.Handle.Complete();
                ScheduledFlow.RemoveAtSwapBack(i);
            }
        }
        _losIntegrationScheduler.TryComplete();
    }
    public void ForceComplete()
    {
        for (int i = ScheduledFlow.Length - 1; i >= 0; i--)
        {
            PathPipelineInfoWithHandle flowHandle = ScheduledFlow[i];
            flowHandle.Handle.Complete();
        }
        ScheduledFlow.Clear();
        _losIntegrationScheduler.ForceComplete();
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
            flowfield.Resize(pathInternalData.FlowFieldLength[0], NativeArrayOptions.ClearMemory);
            flowData.FlowField = flowfield;

            UnsafeLOSBitmap losmap = flowData.LOSMap;
            losmap.Resize(pathInternalData.FlowFieldLength[0], NativeArrayOptions.ClearMemory);
            flowData.LOSMap = losmap;

            flowDataList[pathIndex] = flowData;
        }
        _flowFieldResizedPaths.Clear();
    }
}
