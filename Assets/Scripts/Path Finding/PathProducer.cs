using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
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
        for(int i = 0; i < ProducedPaths.Count; i++)
        {
            if (ProducedPaths[i].State == PathState.ToBeUpdated)
            {
                NativeArray<Vector3> sources = new NativeArray<Vector3>(ProducedPaths[i].Sources, Allocator.Persistent);
                Vector3 destination = ProducedPaths[i].Destination;
                ProducedPaths[i].SetState(PathState.ToBeDisposed);
                _pathfindingManager.SetDestination(sources, destination);
            }
            if(ProducedPaths[i].State == PathState.ToBeDisposed && ProducedPaths[i].IsCalculated)
            {
                ProducedPaths[i].Dispose();
                ProducedPaths.RemoveAt(i);
            }
        }
    }
    public Path ProducePath(NativeArray<Vector3> sources, Vector3 destination, int offset)
    {
        int2 destinationIndex = new int2(Mathf.FloorToInt(destination.x / _tileSize), Mathf.FloorToInt(destination.z / _tileSize));
        int destionationIndexFlat = destinationIndex.y * _columnAmount + destinationIndex.x;
        CostField pickedCostField = _costFieldProducer.GetCostFieldWithOffset(offset);
        if (pickedCostField.Costs[destionationIndexFlat] == byte.MaxValue) { return null; }
        NativeArray<float> portalDistances = new NativeArray<float>(pickedCostField.FieldGraph.PortalNodes.Length, Allocator.Persistent);
        NativeArray<int> connectionIndicies = new NativeArray<int>(pickedCostField.FieldGraph.PortalNodes.Length, Allocator.Persistent);
        NativeList<PortalSequence> portalSequence = new NativeList<PortalSequence>(Allocator.Persistent);
        NativeArray<PortalMark> portalMarks = new NativeArray<PortalMark>(pickedCostField.FieldGraph.PortalNodes.Length, Allocator.Persistent);
        NativeQueue<LocalIndex1d> blockedWaveFronts = new NativeQueue<LocalIndex1d>(Allocator.Persistent);
        NativeQueue<LocalIndex2d> intqueue = new NativeQueue<LocalIndex2d>(Allocator.Persistent);
        NativeArray<int> sectorMarks = new NativeArray<int>(pickedCostField.FieldGraph.SectorNodes.Length, Allocator.Persistent);
        NativeArray<int> sectorCount = new NativeArray<int>(1, Allocator.Persistent);
        NativeList<IntegrationFieldSector> integrationField = new NativeList<IntegrationFieldSector>(Allocator.Persistent);
        integrationField.Add(new IntegrationFieldSector());
        NativeList<FlowFieldSector> flowField = new NativeList<FlowFieldSector>(Allocator.Persistent);
        flowField.Add(new FlowFieldSector());
        Path producedPath = new Path()
        {
            BlockedWaveFronts = blockedWaveFronts,
            Sources = sources,
            Destination = destination,
            State = PathState.Clean,
            Offset = offset,
            PortalDistances = portalDistances,
            ConnectionIndicies = connectionIndicies,
            PortalSequence = portalSequence,
            PortalMarks = portalMarks,
            IntegrationField = integrationField,
            FlowField = flowField,
            intqueue = intqueue,
            SectorMarks = sectorMarks,
        };
        if(ProducedPaths.Count != 0)
        {
            ProducedPaths.Last().SetState(PathState.ToBeDisposed);
        }
        ProducedPaths.Add(producedPath);

        //TRAVERSAL JOB
        FieldGraphTraversalJob traversalJob = GetTraversalJob();
        traversalJob.Schedule().Complete();
        //SECTOR PREPERATION
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
        //RESET JOB
        NativeList<JobHandle> resetHandles = new NativeList<JobHandle>(Allocator.Temp);
        for(int i = 1; i < integrationField.Length; i++)
        {
            resetHandles.Add(GetResetFieldJob(integrationField[i].integrationSector).Schedule(_sectorTileAmount * _sectorTileAmount, 512));
        }
        JobHandle.CombineDependencies(resetHandles).Complete();
        //LOS JOB
        LOSJob reflosjob = GetRefLosJob();
        reflosjob.Schedule().Complete();
        //INT JOB
        IntFieldJob intjob = GetRefIntegrationJob();
        intjob.Schedule().Complete();
        //FLOWFIELD JOB
        NativeList<JobHandle> flowfieldHandles = new NativeList<JobHandle>(Allocator.Temp);
        for (int i = 1; i < flowField.Length; i++)
        {
            flowfieldHandles.Add(GetFlowFieldJob(flowField[i].flowfieldSector, flowField[i].sectorIndex1d).Schedule(_sectorTileAmount * _sectorTileAmount, 512));
        }
        JobHandle.CombineDependencies(flowfieldHandles).Complete();
        producedPath.IsCalculated = true;
        return producedPath;

        //HELPERS
        FieldGraphTraversalJob GetTraversalJob()
        {
            return new FieldGraphTraversalJob()
            {
                FieldColAmount = _columnAmount,
                PortalNodes = pickedCostField.FieldGraph.PortalNodes,
                SecToWinPtrs = pickedCostField.FieldGraph.SecToWinPtrs,
                WindowNodes = pickedCostField.FieldGraph.WindowNodes,
                WinToSecPtrs = pickedCostField.FieldGraph.WinToSecPtrs,
                FieldRowAmount = _rowAmount,
                FieldTileSize = _tileSize,
                TargetPosition = destination,
                SourcePositions = sources,
                PorPtrs = pickedCostField.FieldGraph.PorToPorPtrs,
                SectorNodes = pickedCostField.FieldGraph.SectorNodes,
                Costs = pickedCostField.Costs,
                SectorTileAmount = _sectorTileAmount,
                SectorMatrixColAmount = _columnAmount / _sectorTileAmount,
                LocalDirections = _costFieldProducer.LocalDirections,
                ConnectionIndicies = connectionIndicies,
                PortalDistances = portalDistances,
                PortalSequence = portalSequence,
                PortalMarks = portalMarks,
                SectorMarks = sectorMarks,
                IntegrationField = integrationField,
                FlowField = flowField,
            };
        }
        IntFieldResetJob GetResetFieldJob(UnsafeList<IntegrationTile> integrationFieldSector)
        {
            return new IntFieldResetJob(integrationFieldSector);
        }
        LOSJob GetRefLosJob()
        {
            return new LOSJob()
            {
                TileSize = _tileSize,
                FieldRowAmount = _rowAmount,
                FieldColAmount = _columnAmount,
                SectorTileAmount = _sectorTileAmount,
                SectorMatrixColAmount = _sectorMatrixColAmount,
                SectorMatrixRowAmount = _sectorMatrixRowAmount,
                Costs = pickedCostField.Costs,
                Target = destinationIndex,
                SectorMarks = sectorMarks,
                IntegrationField = integrationField,
                IntegrationQueue = intqueue,
                BlockedWaveFronts = blockedWaveFronts,
            };
        }
        IntFieldJob GetRefIntegrationJob()
        {
            return new IntFieldJob()
            {
                IntegrationQueue = blockedWaveFronts,
                Costs = pickedCostField.Costs,
                IntegrationField = integrationField,
                SectorMarks = sectorMarks,
                SectorColAmount = _sectorTileAmount,
                SectorMatrixColAmount = _sectorMatrixColAmount,
                FieldColAmount = _columnAmount,
                FieldRowAmount = _rowAmount,
            };
        }
        /*
        IntFieldPrepJob GetPreperationJob()
        {
            return new IntFieldPrepJob()
            {
                PickedSectors = pickedSectors,
                FieldColAmount = _columnAmount,
                FieldRowAmount = _rowAmount,
                IntegrationField = integrationField,
                SectorNodes = pickedCostField.FieldGraph.SectorNodes,
                Costs = pickedCostField.Costs,
                SectorMarks = sectorMarks,
                SectorTileAmount = _pathfindingManager.SectorTileAmount,
                SectorMatrixColAmount = _columnAmount / _sectorTileAmount
            };
        }
        LOSJob GetLOSJob()
        {
            return new LOSJob()
            {
                BlockedWaveFronts = blockedWaveFronts,
                TileSize = _tileSize,
                FieldRowAmount = _rowAmount,
                FieldColAmount = _columnAmount,
                Costs = pickedCostField.Costs,
                Directions = _costFieldProducer.Directions,
                InitialWaveFront = destionationIndexFlat,
                IntegrationField = integrationField,
            };
        }/*
        IntFieldJob GetIntegrationJob()
        {
            return new IntFieldJob()
            {
                IntegrationQueue = blockedWaveFronts,
                Costs = pickedCostField.Costs,
                Directions = _costFieldProducer.Directions,
                IntegrationField = integrationField,
            };
        }*/
        FlowFieldJob GetFlowFieldJob(UnsafeList<FlowData> flowfieldSector, int sectorIndex1d)
        {
            return new FlowFieldJob()
            {
                SectorColAmount = _sectorTileAmount,
                SectorMatrixColAmount = _sectorMatrixColAmount,
                SectorMatrixRowAmount = _sectorMatrixRowAmount,
                SectorRowAmount = _sectorTileAmount,
                SectorIndex1d = sectorIndex1d,
                SectorMarks = sectorMarks,
                FlowSector = flowfieldSector,
                IntegrationField = integrationField,
            };
        }
    }
    public void MarkSectors(NativeList<int> editedSectorIndicies)
    {/*
        for(int i = 0; i < ProducedPaths.Count; i++)
        {
            NativeList<int> pickedSectors = ProducedPaths[i].PickedSectors;
            for (int j = 0; j < editedSectorIndicies.Length; j++)
            {
                if (pickedSectors.Contains(editedSectorIndicies[j]))
                {
                    ProducedPaths[i].SetState(PathState.ToBeUpdated);
                }
            }
        }*/
    }
}