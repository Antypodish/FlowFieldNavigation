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
    }
    public void Update()
    {
        for (int i = ProducedPaths.Count - 1; i >= 0; i--)
        {
            if (ProducedPaths[i].State == PathState.ToBeDisposed && ProducedPaths[i].IsCalculated)
            {
                ProducedPaths[i].Dispose();
                ProducedPaths[ProducedPaths.Count - 1].Id = i;
                ProducedPaths.RemoveAtSwapBack(i);
            }
        }
    }
    public PortalTraversalJobPack GetPortalTraversalJobPack(NativeArray<Vector3> sources, Vector2 destination, int offset)
    {
        int2 destinationIndex = new int2(Mathf.FloorToInt(destination.x / _tileSize), Mathf.FloorToInt(destination.y / _tileSize));
        int destionationIndexFlat = destinationIndex.y * _columnAmount + destinationIndex.x;
        CostField pickedCostField = _costFieldProducer.GetCostFieldWithOffset(offset);

        //returns portal pack with path=null if target is unwalkable
        if (pickedCostField.CostsG[destionationIndexFlat] == byte.MaxValue) { return new PortalTraversalJobPack(); }

        NativeList<int> portalSequence = new NativeList<int>(Allocator.Persistent);
        NativeList<int> portalSequenceBorders = new NativeList<int>(Allocator.Persistent);
        NativeArray<PortalTraversalData> portalTraversalDataArray = new NativeArray<PortalTraversalData>(pickedCostField.FieldGraph.PortalNodes.Length, Allocator.Persistent);
        NativeArray<DijkstraTile> targetSectorCosts = new NativeArray<DijkstraTile>(_sectorTileAmount * _sectorTileAmount, Allocator.Persistent);
        NativeQueue<LocalIndex1d> blockedWaveFronts = new NativeQueue<LocalIndex1d>(Allocator.Persistent);
        UnsafeList<int> sectorToPicked = new UnsafeList<int>(pickedCostField.FieldGraph.SectorNodes.Length, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        sectorToPicked.Length = pickedCostField.FieldGraph.SectorNodes.Length;
        NativeList<int> pickedToSector = new NativeList<int>(Allocator.Persistent);
        NativeArray<int> flowFieldLength = new NativeArray<int>(1, Allocator.Persistent);

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
    public FlowFieldHandle SchedulePathProductionJob(Path path)
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
            BlockedWaveFronts = path.BlockedWaveFronts,
        };

        //INTEGRATION
        IntegrationFieldJob intjob = new IntegrationFieldJob()
        {
            Target = destinationIndex,
            WaveFrontQueue = path.BlockedWaveFronts,
            Costs = pickedCostField.CostsL,
            IntegrationField = path.IntegrationField,
            SectorMarks = path.SectorToPicked,
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

        JobHandle resetHandle = resetJob.Schedule(resetJob.IntegrationField.Length, 32);
        JobHandle losHandle = losjob.Schedule(resetHandle);
        JobHandle integrationHandle = intjob.Schedule(losHandle);

        return new FlowFieldHandle()
        {
            Handle = ffJob.Schedule(ffJob.FlowField.Length, 256, integrationHandle),
            path = path,
        };
    }
    public void SetPortalAdditionTraversalHandles(NativeList<AgentMovementData> movDataArray, List<PortalAdditionTraversalHandle> portalAdditionTraversalHandles)
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
                PortalAdditionTraversalHandle handle = new PortalAdditionTraversalHandle(travJob.Schedule(), path);
                handle.Handle.Complete();
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
        IntegrationFieldAdditionJob intAddJob = new IntegrationFieldAdditionJob()
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