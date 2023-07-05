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
        Stopwatch sw = new Stopwatch();
        int2 destinationIndex = new int2(Mathf.FloorToInt(destination.x / _tileSize), Mathf.FloorToInt(destination.y / _tileSize));
        int destionationIndexFlat = destinationIndex.y * _columnAmount + destinationIndex.x;
        CostField pickedCostField = _costFieldProducer.GetCostFieldWithOffset(offset);
        if (pickedCostField.CostsG[destionationIndexFlat] == byte.MaxValue) { return null; }

        NativeList<int> portalSequence = new NativeList<int>(Allocator.Persistent);
        NativeList<int> portalSequenceBorders = new NativeList<int>(Allocator.Persistent);
        NativeArray<PortalTraversalData> portalTraversalDataArray = new NativeArray<PortalTraversalData>(pickedCostField.FieldGraph.PortalNodes.Length, Allocator.Persistent);
        NativeArray<DijkstraTile> targetSectorCosts = new NativeArray<DijkstraTile>(_sectorTileAmount * _sectorTileAmount, Allocator.Persistent);
        NativeQueue<LocalIndex1d> blockedWaveFronts = new NativeQueue<LocalIndex1d>(Allocator.Persistent);
        NativeQueue<LocalIndex1d> intqueue = new NativeQueue<LocalIndex1d>(Allocator.Persistent);
        NativeArray<int> sectorMarks = new NativeArray<int>(pickedCostField.FieldGraph.SectorNodes.Length, Allocator.Persistent);
        NativeList<IntegrationFieldSector> integrationField = new NativeList<IntegrationFieldSector>(Allocator.Persistent);
        NativeList<FlowFieldSector> flowField = new NativeList<FlowFieldSector>(Allocator.Persistent);
        integrationField.Add(new IntegrationFieldSector());
        flowField.Add(new FlowFieldSector());

        Path producedPath = new Path()
        {
            PortalSequenceBorders = portalSequenceBorders,
            TargetIndex = destinationIndex,
            TargetSectorCosts = targetSectorCosts,
            BlockedWaveFronts = blockedWaveFronts,
            Sources = sources,
            Destination = destination,
            State = PathState.Clean,
            Offset = offset,
            PortalSequence = portalSequence,
            PortalTraversalDataArray = portalTraversalDataArray,
            IntegrationField = integrationField,
            FlowField = flowField,
            intqueue = intqueue,
            SectorMarks = sectorMarks,
        };
        ProducedPaths.Add(producedPath);
       
        //TRAVERSAL
        FieldGraphTraversalJob traversalJob = GetTraversalJob();
        traversalJob.Schedule().Complete();

        for (int i = 1; i < integrationField.Length; i++)
        {
            IntegrationFieldSector intSector = integrationField[i];
            intSector.integrationSector = new UnsafeList<IntegrationTile>(_sectorTileAmount * _sectorTileAmount, Allocator.Persistent);
            intSector.integrationSector.Length = _sectorTileAmount * _sectorTileAmount;
            integrationField[i] = intSector;

            FlowFieldSector flowSector = flowField[i];
            flowSector.flowfieldSector = new UnsafeList<FlowData>(_sectorTileAmount * _sectorTileAmount, Allocator.Persistent);
            flowSector.flowfieldSector.Length = _sectorTileAmount * _sectorTileAmount;
            flowField[i] = flowSector;
        }

        //INT FIELD RESET
        NativeList<JobHandle> resetHandles = new NativeList<JobHandle>(Allocator.Temp);
        for (int i = 1; i < integrationField.Length; i++)
        {
            resetHandles.Add(GetResetFieldJob(integrationField[i].integrationSector).Schedule(_sectorTileAmount * _sectorTileAmount, 512));
        }
        JobHandle resetHandle = JobHandle.CombineDependencies(resetHandles);
        resetHandle.Complete();

        //LOS
        LOSJob losjob = GetLosJob();
        JobHandle losHandle = losjob.Schedule(resetHandle);
        losHandle.Complete();
        sw.Start();

        //INTEGRATION
        IntFieldJob intjob = GetIntegrationJob();
        JobHandle integrationHandle = intjob.Schedule();
        integrationHandle.Complete();
        sw.Stop();
        //FLOW FIELD
        NativeList<JobHandle> flowfieldHandles = new NativeList<JobHandle>(Allocator.Temp);
        for (int i = 1; i < flowField.Length; i++)
        {
            flowfieldHandles.Add(GetFlowFieldJob(flowField[i].flowfieldSector, flowField[i].sectorIndex1d).Schedule(_sectorTileAmount * _sectorTileAmount, 512));
        }
        JobHandle.CombineDependencies(flowfieldHandles).Complete();

        producedPath.IsCalculated = true;
        UnityEngine.Debug.Log(sw.Elapsed.TotalMilliseconds);
        return producedPath;

        //HELPERS
        FieldGraphTraversalJob GetTraversalJob()
        {
            return new FieldGraphTraversalJob()
            {
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
                SectorTileAmount = _sectorTileAmount,
                SectorMatrixColAmount = _columnAmount / _sectorTileAmount,
                LocalDirections = _costFieldProducer.SectorDirections,
                PortalSequence = portalSequence,
                SectorMarks = sectorMarks,
                IntegrationField = integrationField,
                FlowField = flowField,
                PortalTraversalDataArray = portalTraversalDataArray,
            };
        }
        IntFieldResetJob GetResetFieldJob(UnsafeList<IntegrationTile> integrationFieldSector)
        {
            return new IntFieldResetJob(integrationFieldSector);
        }
        LOSJob GetLosJob()
        {
            return new LOSJob()
            {
                TileSize = _tileSize,
                FieldRowAmount = _rowAmount,
                FieldColAmount = _columnAmount,
                SectorTileAmount = _sectorTileAmount,
                SectorMatrixColAmount = _sectorMatrixColAmount,
                SectorMatrixRowAmount = _sectorMatrixRowAmount,
                Costs = pickedCostField.CostsG,
                Target = destinationIndex,
                SectorMarks = sectorMarks,
                Directions = pickedCostField.LocalDirections,
                IntegrationField = integrationField,
                IntegrationQueue = intqueue,
                BlockedWaveFronts = blockedWaveFronts,
            };
        }
        IntFieldJob GetIntegrationJob()
        {
            return new IntFieldJob()
            {
                Target = destinationIndex,
                IntegrationQueue = blockedWaveFronts,
                Costs = pickedCostField.CostsL,
                LocalDirections = pickedCostField.LocalDirections,
                IntegrationField = integrationField,
                SectorMarks = sectorMarks,
                SectorColAmount = _sectorTileAmount,
                SectorMatrixColAmount = _sectorMatrixColAmount,
                FieldColAmount = _columnAmount,
                FieldRowAmount = _rowAmount,
            };
        }
        FlowFieldJob GetFlowFieldJob(UnsafeList<FlowData> flowfieldSector, int sectorIndex1d)
        {
            return new FlowFieldJob()
            {
                SectorColAmount = _sectorTileAmount,
                SectorMatrixColAmount = _sectorMatrixColAmount,
                SectorMatrixRowAmount = _sectorMatrixRowAmount,
                SectorRowAmount = _sectorTileAmount,
                SectorIndex1d = sectorIndex1d,
                Directions = pickedCostField.LocalDirections,
                SectorMarks = sectorMarks,
                FlowSector = flowfieldSector,
                IntegrationField = integrationField,
            };
        }
    }
    public void AddSectorToPath(Path path, NativeList<int> sectorIndicies)
    {
        CostField pickedCostField = _costFieldProducer.GetCostFieldWithOffset(path.Offset);
        int newSectorStartIndex = path.FlowField.Length;
        NativeList<LocalIndex1d> integrationStartIndicies = new NativeList<LocalIndex1d>(Allocator.TempJob);  
        
        //TRAVERSAL
        FlowFieldAdditionTraversalJob travJob = GetAdditionTraversalJob();
        travJob.Schedule().Complete();

        //ADDING NEW SECTORS
        for(int i = newSectorStartIndex; i < path.FlowField.Length; i++)
        {
            IntegrationFieldSector intSector = path.IntegrationField[i];
            intSector.integrationSector = new UnsafeList<IntegrationTile>(_sectorTileAmount * _sectorTileAmount, Allocator.Persistent);
            intSector.integrationSector.Length = _sectorTileAmount * _sectorTileAmount;
            path.IntegrationField[i] = intSector;

            FlowFieldSector flowSector = path.FlowField[i];
            flowSector.flowfieldSector = new UnsafeList<FlowData>(_sectorTileAmount * _sectorTileAmount, Allocator.Persistent);
            flowSector.flowfieldSector.Length = _sectorTileAmount * _sectorTileAmount;
            path.FlowField[i] = flowSector;
        }
        //INT RESET
        for(int i = newSectorStartIndex; i < path.IntegrationField.Length; i++)
        {
            GetResetFieldJob(path.IntegrationField[i].integrationSector).Schedule(path.IntegrationField[i].integrationSector.Length, 512).Complete();
        }
        //INT
        IntegrationFieldAdditionJob intAddJob = GetIntegrationAdditionJob();
        JobHandle intHandle = intAddJob.Schedule();
        intHandle.Complete();
        
        //FLOW FIELD
        NativeList<JobHandle> flowfieldHandles = new NativeList<JobHandle>(Allocator.Temp);
        for (int i = 1; i < path.FlowField.Length; i++)
        {
            flowfieldHandles.Add(GetFlowFieldJob(path.FlowField[i].flowfieldSector, path.FlowField[i].sectorIndex1d).Schedule(_sectorTileAmount * _sectorTileAmount, 512, intHandle));
        }
        JobHandle jombined = JobHandle.CombineDependencies(flowfieldHandles);
        jombined.Complete();
        sectorIndicies.Dispose();
        integrationStartIndicies.Dispose();

        FlowFieldAdditionTraversalJob GetAdditionTraversalJob()
        {
            return new FlowFieldAdditionTraversalJob()
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
                SectorMarks = path.SectorMarks,
                IntegrationField = path.IntegrationField,
                FlowField = path.FlowField,
                IntegrationStartIndicies = integrationStartIndicies,
            };
        }
        IntFieldResetJob GetResetFieldJob(UnsafeList<IntegrationTile> integrationFieldSector)
        {
            return new IntFieldResetJob(integrationFieldSector);
        }
        IntegrationFieldAdditionJob GetIntegrationAdditionJob()
        {
            return new IntegrationFieldAdditionJob()
            {
                StartIndicies = integrationStartIndicies,
                IntegrationQueue = new NativeQueue<LocalIndex1d>(Allocator.Persistent),
                Costs = pickedCostField.CostsL,
                LocalDirections = pickedCostField.LocalDirections,
                IntegrationField = path.IntegrationField,
                SectorMarks = path.SectorMarks,
                SectorColAmount = _sectorTileAmount,
                SectorMatrixColAmount = _sectorMatrixColAmount,
                FieldColAmount = _columnAmount,
                FieldRowAmount = _rowAmount,
            };
        }
        FlowFieldJob GetFlowFieldJob(UnsafeList<FlowData> flowfieldSector, int sectorIndex1d)
        {
            return new FlowFieldJob()
            {
                SectorColAmount = _sectorTileAmount,
                SectorMatrixColAmount = _sectorMatrixColAmount,
                SectorMatrixRowAmount = _sectorMatrixRowAmount,
                SectorRowAmount = _sectorTileAmount,
                SectorIndex1d = sectorIndex1d,
                Directions = pickedCostField.LocalDirections,
                SectorMarks = path.SectorMarks,
                FlowSector = flowfieldSector,
                IntegrationField = path.IntegrationField,
            };
        }
    }
}