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

    NativeList<HandleWithPathIndex> ScheduledFlow;
    NativeList<int> _flowFieldResizedPaths;
    NativeList<int> _losCalculatedPaths;
    NativeList<FlowFieldCalculationBufferParent> _flowFieldCalculationBuffers;

    public FlowCalculationScheduler(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
        _pathProducer = pathfindingManager.PathProducer;
        ScheduledFlow = new NativeList<HandleWithPathIndex>(Allocator.Persistent);
        _losCalculatedPaths = new NativeList<int>(Allocator.Persistent);
        _flowFieldCalculationBuffers = new NativeList<FlowFieldCalculationBufferParent>(Allocator.Persistent);
        _flowFieldResizedPaths = new NativeList<int>(Allocator.Persistent);
    }

    public void ScheduleFlow(int pathIndex)
    {
        Path path = _pathProducer.ProducedPaths[pathIndex];
        CostField pickedCostField = _pathfindingManager.FieldProducer.GetCostFieldWithOffset(path.Offset);

        int lastIntegrationFieldLength = path.IntegrationField.Length;
        int curIntegrationFieldLength = path.FlowFieldLength[0];
        if(lastIntegrationFieldLength != curIntegrationFieldLength)
        {
            path.IntegrationField.Resize(curIntegrationFieldLength, NativeArrayOptions.UninitializedMemory);
            //RESET NEW INT FIELD INDICIES
            IntegrationFieldResetJob resJob = new IntegrationFieldResetJob()
            {
                StartIndex = lastIntegrationFieldLength,
                IntegrationField = path.IntegrationField,
            };
            resJob.Schedule().Complete();
            _flowFieldResizedPaths.Add(pathIndex);
        }


        JobHandle losHandle = new JobHandle();
        bool requestedSectorWithinLOS = (path.SectorWithinLOSState[0] & SectorsWihinLOSArgument.RequestedSectorWithinLOS) == SectorsWihinLOSArgument.RequestedSectorWithinLOS;
        bool addedSectorWithinLOS = (path.SectorWithinLOSState[0] & SectorsWihinLOSArgument.AddedSectorWithinLOS) == SectorsWihinLOSArgument.AddedSectorWithinLOS;
        bool losCalculated = path.LOSCalculated();
        if(losCalculated && addedSectorWithinLOS)
        {
            LOSCleanJob losClean = new LOSCleanJob()
            {
                SectorColAmount = FlowFieldUtilities.SectorColAmount,
                SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
                SectorMatrixRowAmount = FlowFieldUtilities.SectorMatrixRowAmount,
                SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
                LOSRange = FlowFieldUtilities.LOSRange,
                Target = path.TargetIndex,

                SectorToPickedTable = path.SectorToPicked,
                IntegrationField = path.IntegrationField,
            };
            JobHandle loscleanHandle = losClean.Schedule();

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
                SectorToPicked = path.SectorToPicked,
                IntegrationField = path.IntegrationField,
                Target = path.TargetIndex,
            };
            losHandle = losjob.Schedule(loscleanHandle);
            _losCalculatedPaths.Add(pathIndex);
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
                SectorToPicked = path.SectorToPicked,
                IntegrationField = path.IntegrationField,
                Target = path.TargetIndex,
            };
            losHandle = losjob.Schedule();
            _losCalculatedPaths.Add(pathIndex);
        }
        path.SectorWithinLOSState[0] = SectorsWihinLOSArgument.None;

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
                SectorToPicked = path.SectorToPicked,
                SectorColAmount = FlowFieldUtilities.SectorColAmount,
                SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
                FieldColAmount = FlowFieldUtilities.FieldColAmount,
                FieldRowAmount = FlowFieldUtilities.FieldRowAmount,
            };
            JobHandle intHandle = intJob.Schedule(losHandle);
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
                SectorToPicked = path.SectorToPicked,
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
        sectorFlowStartIndiciesToCalculateFlow.Dispose();
        sectorFlowStartIndiciesToCalculateIntegration.Dispose();
        //PUSH BUFFER PARENT TO THE LIST
        FlowFieldCalculationBufferParent parent = new FlowFieldCalculationBufferParent()
        {
            PathIndex = pathIndex,
            BufferParent = bufferParent,
        };
        _flowFieldCalculationBuffers.Add(parent);

        JobHandle flowFieldCombinedHandle = JobHandle.CombineDependencies(flowfieldHandles);
        //IF LOS IS REQUESTED BUT NO FLOW FIELD OR INTEGRATION... YEAH...
        JobHandle combinedHandle = JobHandle.CombineDependencies(flowFieldCombinedHandle, losHandle);

        if (FlowFieldUtilities.DebugMode) { combinedHandle.Complete(); }
        ScheduledFlow.Add(new HandleWithPathIndex(combinedHandle, pathIndex));
    }
    public void TryComplete()
    {
        for (int i = ScheduledFlow.Length - 1; i >= 0; i--)
        {
            HandleWithPathIndex flowHandle = ScheduledFlow[i];
            if (flowHandle.Handle.IsCompleted)
            {
                flowHandle.Handle.Complete();
                ScheduledFlow.RemoveAtSwapBack(i);
            }
        }
    }
    public void ForceComplete()
    {
        for (int i = ScheduledFlow.Length - 1; i >= 0; i--)
        {
            HandleWithPathIndex flowHandle = ScheduledFlow[i];
            flowHandle.Handle.Complete();
        }
        ScheduledFlow.Clear();
        RefreshResizedFlowFieldLengths();
        TransferAllFlowFieldCalculationsToTheFlowFields();
        DisposeFlowFieldCalculationBuffers();
    }
    void TransferAllFlowFieldCalculationsToTheFlowFields()
    {
        NativeList<JobHandle> handles = new NativeList<JobHandle>(Allocator.Temp);
        List<Path> producedPaths = _pathProducer.ProducedPaths;
        for (int i = 0; i < _losCalculatedPaths.Length; i++)
        {
            Path path = producedPaths[_losCalculatedPaths[i]];
            LOSTransferJob losTransfer = new LOSTransferJob()
            {
                SectorColAmount = FlowFieldUtilities.SectorColAmount,
                SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
                SectorMatrixRowAmount = FlowFieldUtilities.SectorMatrixRowAmount,
                SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
                LOSRange = FlowFieldUtilities.LOSRange,
                SectorToPickedTable = path.SectorToPicked,
                LOSBitmap = path.LOSMap,
                IntegrationField = path.IntegrationField,
                Target = path.TargetIndex,
            };
            handles.Add(losTransfer.Schedule());
        }
        _losCalculatedPaths.Clear();
        for (int i = 0; i < _flowFieldCalculationBuffers.Length; i++)
        {
            FlowFieldCalculationBufferParent parent = _flowFieldCalculationBuffers[i];
            int pathIndex = parent.PathIndex;

            FlowFieldCalculationTransferJob transferJob = new FlowFieldCalculationTransferJob()
            {
                CalculationBufferParent = parent,
                FlowField = producedPaths[pathIndex].FlowField,
            };
            handles.Add(transferJob.Schedule());
        }
        
        for(int i = 0; i < handles.Length; i++)
        {
            handles[i].Complete();
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
        for(int i = 0; i < _flowFieldResizedPaths.Length; i++)
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
