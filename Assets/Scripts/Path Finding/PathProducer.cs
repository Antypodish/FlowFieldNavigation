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
        //NativeList<int> pickedSectors = new NativeList<int>(Allocator.Persistent);
        NativeArray<PortalMark> portalMarks = new NativeArray<PortalMark>(pickedCostField.FieldGraph.PortalNodes.Length, Allocator.Persistent);
        //NativeArray<IntegrationTile> integrationField = new NativeArray<IntegrationTile>(pickedCostField.Costs.Length, Allocator.Persistent);
        NativeArray<FlowData> flowField = new NativeArray<FlowData>(pickedCostField.Costs.Length, Allocator.Persistent);
        NativeQueue<int> blockedWaveFronts = new NativeQueue<int>(Allocator.Persistent);
        NativeQueue<LocalIndex> intqueue = new NativeQueue<LocalIndex>(Allocator.Persistent);
        NativeArray<int> sectorMarks = new NativeArray<int>(pickedCostField.FieldGraph.SectorNodes.Length, Allocator.Persistent);
        NativeArray<int> sectorCount = new NativeArray<int>(1, Allocator.Persistent);
        NativeList<UnsafeList<IntegrationTile>> integrationField = new NativeList<UnsafeList<IntegrationTile>>(Allocator.Persistent);
        integrationField.Add(new UnsafeList<IntegrationTile>(1, Allocator.Persistent));
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
        //RESET JOB
        NativeList<JobHandle> resetHandles = new NativeList<JobHandle>(Allocator.Temp);
        for(int i = 0; i < sectorCount[0]; i++)
        {
            UnsafeList<IntegrationTile> integrationFieldSector = new UnsafeList<IntegrationTile>(_sectorTileAmount * _sectorTileAmount, Allocator.Persistent);
            integrationFieldSector.Length = _sectorTileAmount * _sectorTileAmount;
            integrationField.Add(integrationFieldSector);
            resetHandles.Add(GetResetFieldJob(integrationFieldSector).Schedule(_sectorTileAmount * _sectorTileAmount, 512));
        }
        JobHandle.CombineDependencies(resetHandles).Complete();
        //LOS JOB
        LOSJobRefactored reflosjob = GetRefLosJob();
        reflosjob.Schedule().Complete();
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
                SectorCount = sectorCount,
                SectorMarks = sectorMarks,
            };
        }
        IntFieldResetJob GetResetFieldJob(UnsafeList<IntegrationTile> integrationFieldSector)
        {
            return new IntFieldResetJob(integrationFieldSector);
        }
        LOSJobRefactored GetRefLosJob()
        {
            return new LOSJobRefactored()
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
        }
        FlowFieldJob GetFlowFieldJob()
        {
            return new FlowFieldJob()
            {
                DirectionData = _costFieldProducer.Directions,
                FlowField = flowField,
                IntegrationField = integrationField
            };
        }*/
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