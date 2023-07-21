using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.VisualScripting.FullSerializer;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class PathProducer
{
    public List<Path> ProducedPaths;

    PathfindingManager _pathfindingManager;
    CostFieldProducer _costFieldProducer;
    PathPreallocator _preallocator;
    int _columnAmount;
    int _rowAmount;
    float _tileSize;
    int _sectorTileAmount;
    int _sectorMatrixColAmount;
    int _sectorMatrixRowAmount;

    public PathProducer(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
        _costFieldProducer = pathfindingManager.CostFieldProducer;
        _columnAmount = pathfindingManager.ColumnAmount;
        _rowAmount = pathfindingManager.RowAmount;
        _tileSize = pathfindingManager.TileSize;
        _sectorTileAmount = pathfindingManager.SectorTileAmount;
        _sectorMatrixColAmount = _columnAmount / _sectorTileAmount;
        _sectorMatrixRowAmount = _rowAmount / _sectorTileAmount;
        ProducedPaths = new List<Path>(1);
        _preallocator = new PathPreallocator(_costFieldProducer, _sectorTileAmount * _sectorTileAmount, _sectorMatrixColAmount * _sectorMatrixRowAmount);
    }
    public void Update()
    {
        for (int i = ProducedPaths.Count - 1; i >= 0; i--)
        {
            Path path = ProducedPaths[i];
            if (path.State == PathState.ToBeDisposed && path.IsCalculated)
            {
                path.Dispose();
                PreallocationPack preallocations = new PreallocationPack()
                {
                    SectorToPicked = path.SectorToPicked,
                    PickedToSector = path.PickedToSector,
                    PortalSequence = path.PortalSequence,
                    PortalSequenceBorders = path.PortalSequenceBorders,
                    PortalTraversalDataArray = path.PortalTraversalDataArray,
                    TargetSectorCosts = path.TargetSectorCosts,
                    FlowFieldLength = path.FlowFieldLength,
                };

                _preallocator.SendPreallocationsBack(ref preallocations, path.Offset);
                ProducedPaths[ProducedPaths.Count - 1].Id = i;
                ProducedPaths.RemoveAtSwapBack(i);
            }
        }
        _preallocator.CheckForDeallocations();
    }
    public PortalTraversalJobPack GetPortalTraversalJobPack(NativeArray<float2> sources, Vector2 destination, int offset)
    {
        int2 destinationIndex = new int2(Mathf.FloorToInt(destination.x / _tileSize), Mathf.FloorToInt(destination.y / _tileSize));
        int destionationIndexFlat = destinationIndex.y * _columnAmount + destinationIndex.x;
        CostField pickedCostField = _costFieldProducer.GetCostFieldWithOffset(offset);

        //returns portal pack with path=null if target is unwalkable
        if (pickedCostField.CostsG[destionationIndexFlat] == byte.MaxValue) { return new PortalTraversalJobPack(); }

        PreallocationPack preallocations = _preallocator.GetPreallocations(offset);
        NativeList<int> portalSequence = preallocations.PortalSequence;
        NativeList<int> portalSequenceBorders = preallocations.PortalSequenceBorders;
        NativeArray<PortalTraversalData> portalTraversalDataArray = preallocations.PortalTraversalDataArray;
        NativeArray<DijkstraTile> targetSectorCosts = preallocations.TargetSectorCosts;
        UnsafeList<int> sectorToPicked = preallocations.SectorToPicked;
        NativeList<int> pickedToSector = preallocations.PickedToSector;
        NativeArray<int> flowFieldLength = preallocations.FlowFieldLength;

        //TRAVERSAL
        PortalNodeTraversalJob traversalJob = new PortalNodeTraversalJob()
        {
            PickedToSector = pickedToSector,
            PortalSequenceBorders = portalSequenceBorders,
            TargetSectorCosts = targetSectorCosts,
            TargetIndex = destinationIndex,
            FieldColAmount = _columnAmount,
            PortalNodes = pickedCostField.FieldGraph.PortalNodes,
            SecToWinPtrs = pickedCostField.FieldGraph.SecToWinPtrs,
            WindowNodes = pickedCostField.FieldGraph.WindowNodes,
            WinToSecPtrs = pickedCostField.FieldGraph.WinToSecPtrs,
            FieldRowAmount = _rowAmount,
            FieldTileSize = _tileSize,
            SourcePositions = sources,
            PorPtrs = pickedCostField.FieldGraph.PorToPorPtrs,
            SectorNodes = pickedCostField.FieldGraph.SectorNodes,
            Costs = pickedCostField.CostsG,
            SectorColAmount = _sectorTileAmount,
            SectorMatrixColAmount = _columnAmount / _sectorTileAmount,
            LocalDirections = _costFieldProducer.SectorDirections,
            PortalSequence = portalSequence,
            SectorToPicked = sectorToPicked,
            FlowFieldLength = flowFieldLength,
            PortalTraversalDataArray = portalTraversalDataArray,
        };

        Path producedPath = new Path()
        {
            Id = ProducedPaths.Count,
            PickedToSector = pickedToSector,
            PortalSequenceBorders = portalSequenceBorders,
            TargetIndex = destinationIndex,
            TargetSectorCosts = targetSectorCosts,
            Sources = sources,
            Destination = destination,
            State = PathState.Clean,
            Offset = offset,
            PortalSequence = portalSequence,
            PortalTraversalDataArray = portalTraversalDataArray,
            SectorToPicked = sectorToPicked,
            FlowFieldLength = flowFieldLength,
            IntegrationStartIndicies = new NativeList<LocalIndex1d>(Allocator.Persistent),
            NewFlowFieldLength = new NativeArray<int>(1, Allocator.Persistent),
        };
        ProducedPaths.Add(producedPath);
        return new PortalTraversalJobPack
        {
            PortalTravJob = traversalJob,
            Path = producedPath,
        };
    }
    public PathHandle SchedulePathProductionJob(Path path)
    {

        CostField pickedCostField = _costFieldProducer.GetCostFieldWithOffset(path.Offset);
        NativeArray<int> flowFieldLength = path.FlowFieldLength;
        path.FlowField = new UnsafeList<FlowData>(flowFieldLength[0], Allocator.Persistent);
        path.FlowField.Length = flowFieldLength[0];
        path.IntegrationField = new NativeList<IntegrationTile>(flowFieldLength[0], Allocator.Persistent);
        path.IntegrationField.Length = flowFieldLength[0];
        int2 destinationIndex = path.TargetIndex;

        //INT FIELD RESET
        IntegrationFieldResetJob resetJob = new IntegrationFieldResetJob()
        {
            IntegrationField = path.IntegrationField,
        };

        //LOS
        LOSJob losjob = new LOSJob()
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
            Directions = pickedCostField.LocalDirections,
            IntegrationField = path.IntegrationField,
            BlockedWaveFronts = path.IntegrationStartIndicies,
        };
        
        //INTEGRATION
        IntegrationFieldJob intjob = new IntegrationFieldJob()
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

        JobHandle resetHandle = resetJob.Schedule();
        JobHandle losHandle = losjob.Schedule(resetHandle);
        JobHandle integrationHandle = intjob.Schedule(losHandle);
        JobHandle ffHandle = ffJob.Schedule(ffJob.FlowField.Length, 256, integrationHandle);
        return new PathHandle()
        {
            Handle = ffHandle,
            Path = path,
        };
    }
    public void SetPortalAdditionTraversalHandles(NativeList<AgentMovementData> movDataArray, List<PathHandle> portalAdditionTraversalHandles, JobHandle dependency)
    {
        for(int i = 0; i < ProducedPaths.Count; i++)
        {
            Path path = ProducedPaths[i];
            if(path.IsCalculated && path.State != PathState.ToBeDisposed)
            {
                CostField pickedCostField = _costFieldProducer.GetCostFieldWithOffset(path.Offset);
                PortalNodeAdditionTraversalJob travJob = new PortalNodeAdditionTraversalJob()
                {
                    PortalSequenceBorders = path.PortalSequenceBorders,
                    FieldColAmount = _columnAmount,
                    TargetIndex = path.TargetIndex,
                    PortalTraversalDataArray = path.PortalTraversalDataArray,
                    TargetSectorCosts = path.TargetSectorCosts,
                    SectorColAmount = _sectorTileAmount,
                    SectorMatrixColAmount = _sectorMatrixColAmount,
                    PortalNodes = pickedCostField.FieldGraph.PortalNodes,
                    SecToWinPtrs = pickedCostField.FieldGraph.SecToWinPtrs,
                    WindowNodes = pickedCostField.FieldGraph.WindowNodes,
                    WinToSecPtrs = pickedCostField.FieldGraph.WinToSecPtrs,
                    PorPtrs = pickedCostField.FieldGraph.PorToPorPtrs,
                    SectorNodes = pickedCostField.FieldGraph.SectorNodes,
                    PortalSequence = path.PortalSequence,
                    SectorToPicked = path.SectorToPicked,
                    PickedToSector = path.PickedToSector,
                    IntegrationStartIndicies = path.IntegrationStartIndicies,
                    ExistingFlowFieldLength = path.FlowField.Length,
                    AgentMovementDataArray = movDataArray,
                    PathId = path.Id,
                    NewFlowFieldLength = path.NewFlowFieldLength,
                };
                PathHandle handle = new PathHandle()
                {
                    Handle = travJob.Schedule(dependency),
                    Path = path,
                };
                portalAdditionTraversalHandles.Add(handle);
            }
        }
    }
    public JobHandle SchedulePathAdditionJob(Path path)
    {
        CostField pickedCostField = _costFieldProducer.GetCostFieldWithOffset(path.Offset);

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