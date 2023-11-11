using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.VisualScripting;
using Unity.VisualScripting.FullSerializer;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

public class PathProducer
{
    public List<Path> ProducedPaths;
    public NativeList<int> ProducedPathSubscribers;
    Stack<int> _removedPathIndicies;
    NativeList<PathData> _pathDataList;
    NativeList<FlowFieldCalculationBufferParent> _flowFieldCalculationBuffers;

    FieldProducer _fieldProducer;
    PathPreallocator _preallocator;
    int _columnAmount;
    int _rowAmount;
    float _tileSize;
    int _sectorTileAmount;
    int _sectorMatrixColAmount;
    int _sectorMatrixRowAmount;

    public PathProducer(PathfindingManager pathfindingManager)
    {
        _fieldProducer = pathfindingManager.FieldProducer;
        _columnAmount = pathfindingManager.ColumnAmount;
        _rowAmount = pathfindingManager.RowAmount;
        _tileSize = pathfindingManager.TileSize;
        _sectorTileAmount = pathfindingManager.SectorColAmount;
        _sectorMatrixColAmount = _columnAmount / _sectorTileAmount;
        _sectorMatrixRowAmount = _rowAmount / _sectorTileAmount;
        ProducedPaths = new List<Path>(1);
        _preallocator = new PathPreallocator(_fieldProducer, _sectorTileAmount * _sectorTileAmount, _sectorMatrixColAmount * _sectorMatrixRowAmount);
        _removedPathIndicies = new Stack<int>();
        _pathDataList = new NativeList<PathData>(Allocator.Persistent);
        ProducedPathSubscribers = new NativeList<int>(Allocator.Persistent);
        _flowFieldCalculationBuffers = new NativeList<FlowFieldCalculationBufferParent>(Allocator.Persistent);
    }
    public void Update()
    {
        for (int i = 0; i < ProducedPaths.Count; i++)
        {
            Path path = ProducedPaths[i];
            if(path.State == PathState.Removed) { continue; }
            int subsciriber = ProducedPathSubscribers[i];
            if(subsciriber == 0 && path.IsCalculated) { path.State = PathState.ToBeDisposed; }
            if (path.State == PathState.ToBeDisposed && path.IsCalculated)
            {
                path.Dispose();
                path.State = PathState.Removed;
                _removedPathIndicies.Push(i);
                PreallocationPack preallocations = new PreallocationPack()
                {
                    SectorToPicked = path.SectorToPicked,
                    PickedToSector = path.PickedToSector,
                    PortalSequence = path.PortalSequence,
                    PortalSequenceBorders = path.PortalSequenceBorders,
                    PortalTraversalDataArray = path.PortalTraversalDataArray,
                    TargetSectorCosts = path.TargetSectorCosts,
                    FlowFieldLength = path.FlowFieldLength,
                    SourcePortalIndexList = path.SourcePortalIndexList,
                    AStartTraverseIndexList = path.AStartTraverseIndexList,
                    TargetSectorPortalIndexList = path.TargetSectorPortalIndexList,
                    PortalTraversalFastMarchingQueue = path.PortalTraversalFastMarchingQueue,
                    SectorStateTable = path.SectorStateTable,
                };

                _preallocator.SendPreallocationsBack(ref preallocations, path.ActiveWaveFrontList, path.FlowField, path.IntegrationField, path.Offset);
            }
        }
        _preallocator.CheckForDeallocations();
    }
    public void DisposeFlowFieldCalculationBuffers()
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
    public NativeList<PathData> GetPathData()
    {
        _pathDataList.Length = ProducedPaths.Count;

        for(int i = 0; i < ProducedPaths.Count; i++)
        {
            Path path = ProducedPaths[i];
            _pathDataList[i] = new PathData()
            {
                Index = i,
                State = path.State,
                AgentCount = 0,
                Target = path.Destination,
                Task = 0,
            };
        }
        return _pathDataList;
    }
    public PortalTraversalJobPack ConstructPath(NativeSlice<float2> positions, PathRequest request)
    {
        int2 destinationIndex = new int2(Mathf.FloorToInt(request.Destination.x / _tileSize), Mathf.FloorToInt(request.Destination.y / _tileSize));
        CostField pickedCostField = _fieldProducer.GetCostFieldWithOffset(request.MaxOffset);
        FieldGraph pickedFieldGraph = _fieldProducer.GetFieldGraphWithOffset(request.MaxOffset);

        PreallocationPack preallocations = _preallocator.GetPreallocations(request.MaxOffset);

        //TRAVERSAL
        NewPortalNodeTraversalJob traversalJob = new NewPortalNodeTraversalJob()
        {
            TargetIndex = destinationIndex,
            FieldColAmount = _columnAmount,
            FieldRowAmount = _rowAmount,
            FieldTileSize = _tileSize,
            SectorColAmount = _sectorTileAmount,
            SectorMatrixColAmount = _columnAmount / _sectorTileAmount,
            PickedToSector = preallocations.PickedToSector,
            PortalSequenceBorders = preallocations.PortalSequenceBorders,
            TargetSectorCosts = preallocations.TargetSectorCosts,
            PortalNodes = pickedFieldGraph.PortalNodes,
            SecToWinPtrs = pickedFieldGraph.SecToWinPtrs,
            WindowNodes = pickedFieldGraph.WindowNodes,
            WinToSecPtrs = pickedFieldGraph.WinToSecPtrs,
            SourcePositions = positions,
            PorPtrs = pickedFieldGraph.PorToPorPtrs,
            SectorNodes = pickedFieldGraph.SectorNodes,
            Costs = pickedCostField.CostsG,
            LocalDirections = _fieldProducer.GetSectorDirections(),
            PortalSequence = preallocations.PortalSequence,
            SectorToPicked = preallocations.SectorToPicked,
            FlowFieldLength = preallocations.FlowFieldLength,
            PortalTraversalDataArray = preallocations.PortalTraversalDataArray,
            SourcePortalIndexList = preallocations.SourcePortalIndexList,
            TargetNeighbourPortalIndicies = preallocations.TargetSectorPortalIndexList,
            AStarTraverseIndexList = preallocations.AStartTraverseIndexList,
            FastMarchingQueue = preallocations.PortalTraversalFastMarchingQueue,
            IslandFields = pickedFieldGraph.IslandFields,
            SectorStateTable = preallocations.SectorStateTable,
        };
        int pathIndex;
        if (_removedPathIndicies.Count != 0) { pathIndex = _removedPathIndicies.Pop(); }
        else { pathIndex = ProducedPaths.Count; }
        Path producedPath = new Path()
        {
            Id = pathIndex,
            PickedToSector = preallocations.PickedToSector,
            PortalSequenceBorders = preallocations.PortalSequenceBorders,
            TargetIndex = destinationIndex,
            TargetSectorCosts = preallocations.TargetSectorCosts,
            Destination = request.Destination,
            State = PathState.Clean,
            Offset = request.MaxOffset,
            PortalSequence = preallocations.PortalSequence,
            PortalTraversalDataArray = preallocations.PortalTraversalDataArray,
            SectorToPicked = preallocations.SectorToPicked,
            FlowFieldLength = preallocations.FlowFieldLength,
            SourcePortalIndexList = preallocations.SourcePortalIndexList,
            AStartTraverseIndexList = preallocations.AStartTraverseIndexList,
            TargetSectorPortalIndexList = preallocations.TargetSectorPortalIndexList,
            PortalTraversalFastMarchingQueue = preallocations.PortalTraversalFastMarchingQueue,
            SectorStateTable = preallocations.SectorStateTable,
            IntegrationStartIndicies = new NativeList<LocalIndex1d>(Allocator.Persistent),
            NewFlowFieldLength = new NativeArray<int>(1, Allocator.Persistent),
        };

        if(ProducedPaths.Count == pathIndex)
        {
            ProducedPaths.Add(producedPath);
            ProducedPathSubscribers.Add(request.AgentCount);
        }
        else
        {
            ProducedPaths[pathIndex] = producedPath;
            ProducedPathSubscribers[pathIndex] = request.AgentCount;
        }


        return new PortalTraversalJobPack
        {
            
            PortalTravJob = traversalJob,
            PathIndex = pathIndex,
        };
    }
    public PathHandle SchedulePathProductionJob(int pathIndex, NativeSlice<float2> sources)
    {
        Path path = ProducedPaths[pathIndex];
        CostField pickedCostField = _fieldProducer.GetCostFieldWithOffset(path.Offset);
        FieldGraph pickedFieldGraph = _fieldProducer.GetFieldGraphWithOffset(path.Offset);
        NativeArray<int> flowFieldLength = path.FlowFieldLength;
        path.FlowField = _preallocator.GetFlowField(flowFieldLength[0]);
        path.IntegrationField = _preallocator.GetIntegrationField(flowFieldLength[0]);
        path.ActiveWaveFrontList = _preallocator.GetActiveWaveFrontListPersistent(path.PickedToSector.Length);
        int2 destinationIndex = path.TargetIndex;
        
        //ACTIVE WAVE FRONT SUBMISSION
        NewActivePortalSubmitJob submitJob = new NewActivePortalSubmitJob()
        {
            SectorColAmount = FlowFieldUtilities.SectorColAmount,
            SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
            SectorMatrixRowAmount = FlowFieldUtilities.SectorMatrixRowAmount,
            SectorRowAmount = FlowFieldUtilities.SectorRowAmount,
            SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
            FieldColAmount = FlowFieldUtilities.FieldColAmount,
            TargetIndex2D = destinationIndex,

            PortalEdges = pickedFieldGraph.PorToPorPtrs,
            SectorToPicked = path.SectorToPicked,
            PickedToSectors = path.PickedToSector,
            PortalSequence = path.PortalSequence,
            PortalSequenceBorders = path.PortalSequenceBorders,
            WinToSecPtrs = pickedFieldGraph.WinToSecPtrs,
            PortalNodes = pickedFieldGraph.PortalNodes,
            WindowNodes = pickedFieldGraph.WindowNodes,
            ActiveWaveFrontListArray = path.ActiveWaveFrontList,
        };
        submitJob.Schedule().Complete();

        NativeList<int> sectorFlowStartIndiciesToCalculateIntegration = new NativeList<int>(Allocator.TempJob);
        NativeList<int> sectorFlowStartIndiciesToCalculateFlow = new NativeList<int>(Allocator.TempJob);
        SourceSectorCalculationJob sectorCalcJob = new SourceSectorCalculationJob()
        {
            SectorColAmount = FlowFieldUtilities.SectorColAmount,
            SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
            SectorSize = FlowFieldUtilities.SectorColAmount * FlowFieldUtilities.TileSize,
            SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
            TargetIndex = path.TargetIndex,
            SectorStateTable = path.SectorStateTable,
            SectorToPickedTable = path.SectorToPicked,
            Sources = sources,
            PortalSequence = path.PortalSequence,
            ActiveWaveFrontListArray = path.ActiveWaveFrontList,
            PortalNodes = pickedFieldGraph.PortalNodes,
            SectorFlowStartIndiciesToCalculateIntegration = sectorFlowStartIndiciesToCalculateIntegration,
            SectorFlowStartIndiciesToCalculateFlow = sectorFlowStartIndiciesToCalculateFlow,
        };
        sectorCalcJob.Schedule().Complete();

        //SCHEDULE INTEGRATION FIELDS
        NativeList<JobHandle> intFieldHandles = new NativeList<JobHandle>(Allocator.Temp);
        for (int i = 0; i < sectorFlowStartIndiciesToCalculateIntegration.Length; i++)
        {
            int sectorStart = sectorFlowStartIndiciesToCalculateIntegration[i];
            int sectorIndex = path.PickedToSector[(sectorStart - 1) / FlowFieldUtilities.SectorTileAmount];
            NativeSlice<IntegrationTile> integrationSector = new NativeSlice<IntegrationTile>(path.IntegrationField, sectorStart, FlowFieldUtilities.SectorTileAmount);
            NewIntegrationFieldJob intJob = new NewIntegrationFieldJob()
            {
                SectorIndex = sectorIndex,
                StartIndicies = submitJob.ActiveWaveFrontListArray[(sectorStart - 1) / FlowFieldUtilities.SectorTileAmount],
                Costs = pickedCostField.CostsL[sectorIndex],
                IntegrationField = integrationSector,
                SectorToPicked = path.SectorToPicked,
                SectorColAmount = _sectorTileAmount,
                SectorMatrixColAmount = _sectorMatrixColAmount,
                FieldColAmount = _columnAmount,
                FieldRowAmount = _rowAmount,
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
                SectorTileAmount = _sectorTileAmount * _sectorTileAmount,
                SectorColAmount = _sectorTileAmount,
                SectorMatrixColAmount = _sectorMatrixColAmount,
                SectorMatrixRowAmount = _sectorMatrixRowAmount,
                SectorMatrixTileAmount = _sectorMatrixRowAmount * _sectorMatrixColAmount,
                SectorStartIndex = sectorStart,
                FieldTileAmount = FlowFieldUtilities.FieldTileAmount,
                FieldColAmount = _columnAmount,
                SectorRowAmount = _sectorTileAmount,
                SectorToPicked = path.SectorToPicked,
                PickedToSector = path.PickedToSector,
                FlowFieldCalculationBuffer = flowFieldCalculationBuffer,
                IntegrationField = path.IntegrationField,
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
            PathIndex = pathIndex,
            BufferParent = bufferParent,
        };
        _flowFieldCalculationBuffers.Add(parent);

        JobHandle flowFieldCombinedHandle = JobHandle.CombineDependencies(flowfieldHandles);
        return new PathHandle()
        {
            Handle = flowFieldCombinedHandle,
            PathIndex = pathIndex,
        };
    }
    public void TransferAllFlowFieldCalculationsToTheFlowFields()
    {
        NativeList<JobHandle> handles = new NativeList<JobHandle>(Allocator.Temp);
        for(int i = 0; i< _flowFieldCalculationBuffers.Length; i++)
        {
            FlowFieldCalculationBufferParent parent = _flowFieldCalculationBuffers[i];
            int pathIndex = parent.PathIndex;

            FlowFieldCalculationTransferJob transferJob = new FlowFieldCalculationTransferJob()
            {
                CalculationBufferParent = parent,
                FlowField = ProducedPaths[pathIndex].FlowField,
            };
            handles.Add(transferJob.Schedule());
        }
        JobHandle.CombineDependencies(handles).Complete();
    }
    public void SetPortalAdditionTraversalHandles(NativeList<OutOfFieldStatus> outOfFieldStatus, List<PathHandle> portalAdditionTraversalHandles, JobHandle dependency)
    {/*
        for(int i = 0; i < ProducedPaths.Count; i++)
        {
            Path path = ProducedPaths[i];
            if(path.IsCalculated && path.State != PathState.ToBeDisposed)
            {
                CostField pickedCostField = _fieldProducer.GetCostFieldWithOffset(path.Offset);
                FieldGraph pickedFieldGraph = _fieldProducer.GetFieldGraphWithOffset(path.Offset);

                PortalNodeAdditionTraversalJob travJob = new PortalNodeAdditionTraversalJob()
                {
                    PortalSequenceBorders = path.PortalSequenceBorders,
                    FieldColAmount = _columnAmount,
                    TargetIndex = path.TargetIndex,
                    PortalTraversalDataArray = path.PortalTraversalDataArray,
                    TargetSectorCosts = path.TargetSectorCosts,
                    SectorColAmount = _sectorTileAmount,
                    SectorMatrixColAmount = _sectorMatrixColAmount,
                    PortalNodes = pickedFieldGraph.PortalNodes,
                    SecToWinPtrs = pickedFieldGraph.SecToWinPtrs,
                    WindowNodes = pickedFieldGraph.WindowNodes,
                    WinToSecPtrs = pickedFieldGraph.WinToSecPtrs,
                    PorPtrs = pickedFieldGraph.PorToPorPtrs,
                    SectorNodes = pickedFieldGraph.SectorNodes,
                    //PortalSequence = path.PortalSequence,
                    SectorToPicked = path.SectorToPicked,
                    PickedToSector = path.PickedToSector,
                    IntegrationStartIndicies = path.IntegrationStartIndicies,
                    ExistingFlowFieldLength = path.FlowField.Length,
                    AgentOutOfFieldStatusList = outOfFieldStatus,
                    PathId = path.Id,
                    NewFlowFieldLength = path.NewFlowFieldLength,
                };
                PathHandle handle = new PathHandle()
                {
                    //Handle = travJob.Schedule(dependency),
                    Path = path,
                };
                portalAdditionTraversalHandles.Add(handle);
            }
        }*/
    }
    public JobHandle SchedulePathAdditionJob(Path path)
    {
        CostField pickedCostField = _fieldProducer.GetCostFieldWithOffset(path.Offset);

        //RESIZING FLOW FIELD
        int oldFieldLength = path.FlowField.Length;
        path.FlowField.Resize(path.NewFlowFieldLength[0], NativeArrayOptions.UninitializedMemory);
        path.IntegrationField.Resize(path.NewFlowFieldLength[0], NativeArrayOptions.UninitializedMemory);

        IntegrationFieldExtensionJob extensionJob = new IntegrationFieldExtensionJob()
        {
            oldFieldLength = oldFieldLength,
            NewIntegrationField = path.IntegrationField,
        };

        //INT
        IntegrationFieldJob intAddJob = new IntegrationFieldJob()
        {
            StartIndicies = path.IntegrationStartIndicies,
            Costs = pickedCostField.CostsL,
            IntegrationField = path.IntegrationField,
            SectorToPicked = path.SectorToPicked,
            SectorColAmount = _sectorTileAmount,
            SectorMatrixColAmount = _sectorMatrixColAmount,
            FieldColAmount = _columnAmount,
            FieldRowAmount = _rowAmount,
        };

        //FLOW FIELD
        FlowFieldJob ffJob = new FlowFieldJob()
        {
            SectorTileAmount = _sectorTileAmount * _sectorTileAmount,
            SectorColAmount = _sectorTileAmount,
            SectorMatrixColAmount = _sectorMatrixColAmount,
            SectorMatrixRowAmount = _sectorMatrixRowAmount,
            SectorRowAmount = _sectorTileAmount,
            SectorToPicked = path.SectorToPicked,
            PickedToSector = path.PickedToSector,
            FlowFieldCalculationBuffer = path.FlowField,
            IntegrationField = path.IntegrationField,
        };

        JobHandle extensionHandle = extensionJob.Schedule();
        JobHandle intHandle = intAddJob.Schedule(extensionHandle);
        JobHandle ffHandle = ffJob.Schedule(path.FlowField.Length, 256, intHandle);

        return ffHandle;
    }
}