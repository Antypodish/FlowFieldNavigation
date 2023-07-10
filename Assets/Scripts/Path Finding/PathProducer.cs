using System;
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
    public DynamicArray<Path> ProducedPaths;

    PathfindingManager _pathfindingManager;
    CostFieldProducer _costFieldProducer;
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
        ProducedPaths = new DynamicArray<Path>(1);
    }
    public void Update()
    {
        for (int i = 0; i < ProducedPaths.Count; i++)
        {
            if (ProducedPaths[i].State == PathState.ToBeDisposed && ProducedPaths[i].IsCalculated)
            {
                ProducedPaths[i].Dispose();
                ProducedPaths.RemoveAt(i);
            }
        }
    }
    public Path ProducePath(NativeArray<Vector3> sources, Vector2 destination, int offset)
    {
        int2 destinationIndex = new int2(Mathf.FloorToInt(destination.x / _tileSize), Mathf.FloorToInt(destination.y / _tileSize));
        int destionationIndexFlat = destinationIndex.y * _columnAmount + destinationIndex.x;
        CostField pickedCostField = _costFieldProducer.GetCostFieldWithOffset(offset);
        if (pickedCostField.CostsG[destionationIndexFlat] == byte.MaxValue) { return null; }

        NativeList<int> portalSequence = new NativeList<int>(Allocator.Persistent);
        NativeList<int> portalSequenceBorders = new NativeList<int>(Allocator.Persistent);
        NativeArray<PortalTraversalData> portalTraversalDataArray = new NativeArray<PortalTraversalData>(pickedCostField.FieldGraph.PortalNodes.Length, Allocator.Persistent);
        NativeArray<DijkstraTile> targetSectorCosts = new NativeArray<DijkstraTile>(_sectorTileAmount * _sectorTileAmount, Allocator.Persistent);
        NativeQueue<LocalIndex1d> blockedWaveFronts = new NativeQueue<LocalIndex1d>(Allocator.TempJob);
        NativeArray<int> sectorToPicked = new NativeArray<int>(pickedCostField.FieldGraph.SectorNodes.Length, Allocator.Persistent);
        NativeList<int> pickedToSector = new NativeList<int>(Allocator.Persistent);
        NativeArray<int> flowFieldLength = new NativeArray<int>(1, Allocator.TempJob);
        NativeArray<IntegrationTile> integrationField;
        NativeArray<FlowData> flowField;

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

        JobHandle traversalHandle = traversalJob.Schedule();
        traversalHandle.Complete();
        flowField = new NativeArray<FlowData>(flowFieldLength[0], Allocator.Persistent);
        integrationField = new NativeArray<IntegrationTile>(flowFieldLength[0], Allocator.Persistent);

        //PATH CREATION
        Path producedPath = new Path()
        {
            BlockedWaveFronts = blockedWaveFronts,
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
            IntegrationField = integrationField,
            FlowField = flowField,
            SectorToPicked = sectorToPicked,
        };
        ProducedPaths.Add(producedPath);

        //INT FIELD RESET
        IntegrationFieldResetJob resetJob = new IntegrationFieldResetJob()
        {
            IntegrationField = integrationField,
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
            SectorToPicked = sectorToPicked,
            Directions = pickedCostField.LocalDirections,
            IntegrationField = integrationField,
            BlockedWaveFronts = blockedWaveFronts,
        };

        //INTEGRATION
        IntegrationFieldJob intjob = new IntegrationFieldJob()
        {
            Target = destinationIndex,
            WaveFrontQueue = blockedWaveFronts,
            Costs = pickedCostField.CostsL,
            IntegrationField = integrationField,
            SectorMarks = sectorToPicked,
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
            SectorToPicked = sectorToPicked,
            PickedToSector = pickedToSector,
            FlowField = flowField,
            IntegrationField = integrationField,
        };

        
        JobHandle resetHandle = resetJob.Schedule(integrationField.Length, 32, traversalHandle);
        JobHandle losHandle = losjob.Schedule(resetHandle);
        JobHandle integrationHandle = intjob.Schedule(losHandle);
        JobHandle ffHandle = ffJob.Schedule(flowField.Length, 256, integrationHandle);
        ffHandle.Complete();

        producedPath.IsCalculated = true;
        producedPath.DisposeTemp();

        return producedPath;
    }
    public void AddSectorToPath(Path path, NativeList<int> sectorIndicies)
    {
        CostField pickedCostField = _costFieldProducer.GetCostFieldWithOffset(path.Offset);
        NativeList<LocalIndex1d> integrationStartIndicies = new NativeList<LocalIndex1d>(Allocator.TempJob);  
        NativeList<IntegrationTile> integrationFieldAddition = new NativeList<IntegrationTile>(Allocator.TempJob);
        NativeList<FlowData> flowFieldAddition = new NativeList<FlowData>(Allocator.TempJob);

        //TRAVERSAL
        PortalNodeAdditionTraversalJob travJob = new PortalNodeAdditionTraversalJob()
        {
            PortalSequenceBorders = path.PortalSequenceBorders,
            FieldColAmount = _columnAmount,
            TargetIndex = path.TargetIndex,
            PortalTraversalDataArray = path.PortalTraversalDataArray,
            NewSectors = sectorIndicies,
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
            IntegrationFieldAddition = integrationFieldAddition,
            FlowFieldAddition = flowFieldAddition,
            IntegrationStartIndicies = integrationStartIndicies,
            ExistingPickedFieldLength = path.FlowField.Length,
        };
        travJob.Schedule().Complete();

        //INT RESET
        IntegrationFieldResetJob resetJob = new IntegrationFieldResetJob()
        {
            IntegrationField = integrationFieldAddition,
        };
        JobHandle resetHandle = resetJob.Schedule(integrationFieldAddition.Length, 32);
        resetHandle.Complete();

        //INT
        IntegrationFieldAdditionJob intAddJob = new IntegrationFieldAdditionJob()
        {
            StartIndicies = integrationStartIndicies,
            Costs = pickedCostField.CostsL,
            IntegrationField = path.IntegrationField,
            IntegrationFieldAddition = integrationFieldAddition,
            FlowField = path.FlowField,
            FlowFieldAddition = flowFieldAddition,
            SectorToPicked = path.SectorToPicked,
            SectorColAmount = _sectorTileAmount,
            SectorMatrixColAmount = _sectorMatrixColAmount,
            FieldColAmount = _columnAmount,
            FieldRowAmount = _rowAmount,
        };
        JobHandle intHandle = intAddJob.Schedule();
        intHandle.Complete();

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
        JobHandle ffHandle = ffJob.Schedule(path.FlowField.Length, 256);
        ffHandle.Complete();

        sectorIndicies.Dispose();
        integrationStartIndicies.Dispose();
        integrationFieldAddition.Dispose();
        flowFieldAddition.Dispose();
    }
}