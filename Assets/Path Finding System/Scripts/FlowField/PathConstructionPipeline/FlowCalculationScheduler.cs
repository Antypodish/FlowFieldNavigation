using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

public class FlowCalculationScheduler
{
    PathfindingManager _pathfindingManager;
    PathContainer _pathProducer;
    LOSIntegrationScheduler _losIntegrationScheduler;
    NativeList<PathPipelineInfoWithHandle> ScheduledFlow;
    NativeList<FlowFieldCalculationBufferParent> _flowFieldCalculationBuffers;
    NativeList<JobHandle> _flowTransferHandles;
    NativeList<int> _flowFieldResizedPaths;
    public FlowCalculationScheduler(PathfindingManager pathfindingManager, LOSIntegrationScheduler losIntegrationScheduler)
    {
        _pathfindingManager = pathfindingManager;
        _pathProducer = pathfindingManager.PathContainer;
        ScheduledFlow = new NativeList<PathPipelineInfoWithHandle>(Allocator.Persistent);
        _flowFieldCalculationBuffers = new NativeList<FlowFieldCalculationBufferParent>(Allocator.Persistent);
        _flowTransferHandles = new NativeList<JobHandle>(Allocator.Persistent);
        _losIntegrationScheduler = losIntegrationScheduler;
        _flowFieldResizedPaths = new NativeList<int>(Allocator.Persistent);
    }

    public void ScheduleFlow(PathPipelineInfoWithHandle pathInfo)
    {
        Path path = _pathProducer.ProducedPaths[pathInfo.PathIndex];
        PathLocationData locationData = _pathProducer.PathLocationDataList[pathInfo.PathIndex];
        CostField pickedCostField = _pathfindingManager.FieldProducer.GetCostFieldWithOffset(path.Offset);
        
        //RESET NEW INT FIELD INDICIES
        int lastIntegrationFieldLength = path.IntegrationField.Length;
        int curIntegrationFieldLength = path.FlowFieldLength[0];
        if (lastIntegrationFieldLength != curIntegrationFieldLength)
        {
            path.IntegrationField.Resize(curIntegrationFieldLength, NativeArrayOptions.UninitializedMemory);
            //RESET NEW INT FIELD INDICIES
            IntegrationFieldResetJob resJob = new IntegrationFieldResetJob()
            {
                StartIndex = lastIntegrationFieldLength,
                IntegrationField = path.IntegrationField,
            };
            resJob.Schedule().Complete();
            _flowFieldResizedPaths.Add(pathInfo.PathIndex);
        }

        //SCHEDULE INTEGRATION FIELDS
        NativeList<JobHandle> intFieldHandles = new NativeList<JobHandle>(Allocator.Temp);
        NativeArray<int> sectorFlowStartIndiciesToCalculateIntegration = path.SectorFlowStartIndiciesToCalculateIntegration;
        NativeArray<int> sectorFlowStartIndiciesToCalculateFlow = path.SectorFlowStartIndiciesToCalculateFlow;
        for (int i = 0; i < sectorFlowStartIndiciesToCalculateIntegration.Length; i++)
        {
            int sectorStart = sectorFlowStartIndiciesToCalculateIntegration[i];
            int sectorIndex = path.PickedToSector[(sectorStart - 1) / FlowFieldUtilities.SectorTileAmount];
            NativeSlice<IntegrationTile> integrationSector = new NativeSlice<IntegrationTile>(path.IntegrationField, sectorStart, FlowFieldUtilities.SectorTileAmount);
            IntegrationFieldJob intJob = new IntegrationFieldJob()
            {
                SectorIndex = sectorIndex,
                StartIndicies = path.ActiveWaveFrontList[(sectorStart - 1) / FlowFieldUtilities.SectorTileAmount],
                Costs = pickedCostField.CostsL[sectorIndex],
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
            if (sectorStart == 0) { UnityEngine.Debug.Log("hüü"); }

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
                PickedToSector = path.PickedToSector,
                FlowFieldCalculationBuffer = flowFieldCalculationBuffer,
                IntegrationField = path.IntegrationField,
                Costs = pickedCostField.CostsL,
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
        List<Path> producedPaths = _pathProducer.ProducedPaths;
        for (int i = 0; i < _flowFieldCalculationBuffers.Length; i++)
        {
            FlowFieldCalculationBufferParent parent = _flowFieldCalculationBuffers[i];
            int pathIndex = parent.PathIndex;

            FlowFieldCalculationTransferJob transferJob = new FlowFieldCalculationTransferJob()
            {
                CalculationBufferParent = parent,
                FlowField = producedPaths[pathIndex].FlowField,
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
        List<Path> producedPaths = _pathProducer.ProducedPaths;
        for (int i = 0; i < _flowFieldResizedPaths.Length; i++)
        {
            int pathIndex = _flowFieldResizedPaths[i];
            Path path = producedPaths[pathIndex];
            UnsafeList<FlowData> flowfield = path.FlowField;
            flowfield.Resize(path.FlowFieldLength[0], NativeArrayOptions.ClearMemory);
            path.FlowField = flowfield;

            UnsafeLOSBitmap losmap = path.LOSMap;
            losmap.Resize(path.FlowFieldLength[0], NativeArrayOptions.ClearMemory);
            path.LOSMap = losmap;
        }
        _flowFieldResizedPaths.Clear();
    }
}
