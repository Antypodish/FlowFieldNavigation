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
using UnityEngine.UIElements;

public class PathProducer
{
    public List<Path> ProducedPaths;
    public NativeList<int> ProducedPathSubscribers;
    Stack<int> _removedPathIndicies;
    NativeList<PathData> _pathDataList;

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
                };

                _preallocator.SendPreallocationsBack(ref preallocations, path.ActiveWaveFrontList, path.Offset);
            }
        }
        _preallocator.CheckForDeallocations();
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
        int destionationIndexFlat = destinationIndex.y * _columnAmount + destinationIndex.x;
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
        };

        Path producedPath = new Path()
        {
            Id = ProducedPaths.Count,
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

            IntegrationStartIndicies = new NativeList<LocalIndex1d>(Allocator.Persistent),
            NewFlowFieldLength = new NativeArray<int>(1, Allocator.Persistent),
        };

        int pathIndex;
        if (_removedPathIndicies.Count != 0)
        {
            pathIndex = _removedPathIndicies.Pop();
            ProducedPaths[pathIndex] = producedPath;
            ProducedPathSubscribers[pathIndex] = request.AgentCount;
        }
        else
        {
            pathIndex = ProducedPaths.Count;
            ProducedPaths.Add(producedPath);
            ProducedPathSubscribers.Add(request.AgentCount);
        }


        return new PortalTraversalJobPack
        {
            PortalTravJob = traversalJob,
            PathIndex = pathIndex,
        };
    }
    public PathHandle SchedulePathProductionJob(int pathIndex)
    {
        Path path = ProducedPaths[pathIndex];
        CostField pickedCostField = _fieldProducer.GetCostFieldWithOffset(path.Offset);
        FieldGraph pickedFieldGraph = _fieldProducer.GetFieldGraphWithOffset(path.Offset);
        NativeArray<int> flowFieldLength = path.FlowFieldLength;
        path.FlowField = new UnsafeList<FlowData>(flowFieldLength[0], Allocator.Persistent);
        path.FlowField.Length = flowFieldLength[0];
        path.IntegrationField = new NativeList<IntegrationTile>(flowFieldLength[0], Allocator.Persistent);
        path.IntegrationField.Length = flowFieldLength[0];
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

            SectorToPicked = path.SectorToPicked,
            PickedToSectors = path.PickedToSector,
            PortalSequence = path.PortalSequence,
            PortalSequenceBorders = path.PortalSequenceBorders,
            WinToSecPtrs = pickedFieldGraph.WinToSecPtrs,
            PortalNodes = pickedFieldGraph.PortalNodes,
            WindowNodes = pickedFieldGraph.WindowNodes,
            ActiveWaveFrontListArray = path.ActiveWaveFrontList,
        };

        //INT FIELD RESET
        IntegrationFieldResetJob resetJob = new IntegrationFieldResetJob()
        {
            IntegrationField = path.IntegrationField,
        };
        resetJob.Schedule().Complete();

        


        //LOS
        /*LOSJob losjob = new LOSJob()
        {
            TileSize = _tileSize,
            FieldRowAmount = _rowAmount,
            FieldColAmount = _columnAmount,
            SectorColAmount = _sectorTileAmount,
            SectorMatrixColAmount = _sectorMatrixColAmount,
            SectorMatrixRowAmount = _sectorMatrixRowAmount,
            Costs = pickedCostField.CostsG,
            Target = destinationIndex,
            SectorToPicked = path.SectorToPicked,
            IntegrationField = path.IntegrationField,
            BlockedWaveFronts = path.IntegrationStartIndicies,
        };*/

        //INTEGRATION
        /*IntegrationFieldJob intjob = new IntegrationFieldJob()
        {
            StartIndicies = path.IntegrationStartIndicies,
            Costs = pickedCostField.CostsL,
            IntegrationField = path.IntegrationField,
            SectorToPicked = path.SectorToPicked,
            SectorColAmount = _sectorTileAmount,
            SectorMatrixColAmount = _sectorMatrixColAmount,
            FieldColAmount = _columnAmount,
            FieldRowAmount = _rowAmount,
        };*/
        
        //FLOW FIELD
        FlowFieldJob ffJob = new FlowFieldJob()
        {
            SectorTileAmount = _sectorTileAmount * _sectorTileAmount,
            SectorColAmount = _sectorTileAmount,
            SectorMatrixColAmount = _sectorMatrixColAmount,
            SectorMatrixRowAmount = _sectorMatrixRowAmount,
            SectorMatrixTileAmount = _sectorMatrixRowAmount * _sectorMatrixColAmount,
            FieldColAmount = _columnAmount,
            SectorRowAmount = _sectorTileAmount,
            SectorToPicked = path.SectorToPicked,
            PickedToSector = path.PickedToSector,
            FlowField = path.FlowField,
            IntegrationField = path.IntegrationField,
        };

        //SCHEDULING
        JobHandle submitHandle = submitJob.Schedule();
        submitHandle.Complete();
        NativeList<JobHandle> intFieldHandles = new NativeList<JobHandle>(path.PickedToSector.Length, Allocator.Temp);
        for (int i = 0; i < path.PickedToSector.Length; i++)
        {
            int sectorIndex = path.PickedToSector[i];
            NativeSlice<IntegrationTile> integrationSector = new NativeSlice<IntegrationTile>(path.IntegrationField, i * _sectorTileAmount  * _sectorTileAmount + 1, _sectorTileAmount * _sectorTileAmount);
            NewIntegrationFieldJob intJob = new NewIntegrationFieldJob()
            {
                SectorIndex = sectorIndex,
                StartIndicies = submitJob.ActiveWaveFrontListArray[i],
                Costs = pickedCostField.CostsL[sectorIndex],
                IntegrationField = integrationSector,
                SectorToPicked = path.SectorToPicked,
                SectorColAmount = _sectorTileAmount,
                SectorMatrixColAmount = _sectorMatrixColAmount,
                FieldColAmount = _columnAmount,
                FieldRowAmount = _rowAmount,
            };
            JobHandle intHandle = intJob.Schedule(submitHandle);
            intFieldHandles.Add(intHandle);
        }

        JobHandle ffHandle = ffJob.Schedule(ffJob.FlowField.Length, 256, JobHandle.CombineDependencies(intFieldHandles));
        return new PathHandle()
        {
            Handle = ffHandle,
            PathIndex = pathIndex,
        };
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
            FlowField = path.FlowField,
            IntegrationField = path.IntegrationField,
        };

        JobHandle extensionHandle = extensionJob.Schedule();
        JobHandle intHandle = intAddJob.Schedule(extensionHandle);
        JobHandle ffHandle = ffJob.Schedule(path.FlowField.Length, 256, intHandle);

        return ffHandle;
    }
}