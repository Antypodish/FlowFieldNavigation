using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
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

    public PathProducer(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
        _costFieldProducer = pathfindingManager.CostFieldProducer;
        _columnAmount = pathfindingManager.ColumnAmount;
        _rowAmount = pathfindingManager.RowAmount;
        _tileSize = pathfindingManager.TileSize;
        _sectorTileAmount = pathfindingManager.SectorTileAmount;
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
        Index2 destinationIndex = new Index2(Mathf.FloorToInt(destination.z / _tileSize), Mathf.FloorToInt(destination.x / _tileSize));
        int destionationIndexFlat = destinationIndex.R * _columnAmount + destinationIndex.C;
        CostField pickedCostField = _costFieldProducer.GetCostFieldWithOffset(offset);
        if (pickedCostField.Costs[destionationIndexFlat] == byte.MaxValue) { return null; }
        NativeArray<float> portalDistances = new NativeArray<float>(pickedCostField.FieldGraph.PortalNodes.Length, Allocator.Persistent);
        NativeArray<int> connectionIndicies = new NativeArray<int>(pickedCostField.FieldGraph.PortalNodes.Length, Allocator.Persistent);
        NativeList<PortalSequence> portalSequence = new NativeList<PortalSequence>(Allocator.Persistent);
        NativeList<int> pickedSectors = new NativeList<int>(Allocator.Persistent);
        NativeArray<PortalMark> portalMarks = new NativeArray<PortalMark>(pickedCostField.FieldGraph.PortalNodes.Length, Allocator.Persistent);
        //NativeArray<IntegrationTile> integrationField = new NativeArray<IntegrationTile>(pickedCostField.Costs.Length, Allocator.Persistent);
        NativeArray<UnsafeList<IntegrationTile>> integrationField = new NativeArray<UnsafeList<IntegrationTile>>(pickedCostField.FieldGraph.SectorNodes.Length, Allocator.Persistent);
        NativeArray<FlowData> flowField = new NativeArray<FlowData>(pickedCostField.Costs.Length, Allocator.Persistent);
        NativeQueue<int> blockedWaveFronts = new NativeQueue<int>(Allocator.Persistent);
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
            PickedSectors = pickedSectors,
            PortalMarks = portalMarks,
            IntegrationField = integrationField,
            FlowField = flowField,
        };
        if(ProducedPaths.Count != 0)
        {
            ProducedPaths.Last().SetState(PathState.ToBeDisposed);
        }
        ProducedPaths.Add(producedPath);
        FieldGraphTraversalJob traversalJob = GetTraversalJob();
        //IntFieldPrepJob prepJob = GetPreperationJob();
        //LOSJob losJob = GetLOSJob();
        //IntFieldJob intJob = GetIntegrationJob();
        //FlowFieldJob flowfieldJob = GetFlowFieldJob();

        traversalJob.Schedule().Complete();
        NativeList<JobHandle> resetHandles = new NativeList<JobHandle>(Allocator.Temp);
        for(int i = 0; i < pickedSectors.Length; i++)
        {
            UnsafeList<IntegrationTile> integrationFieldSector = new UnsafeList<IntegrationTile>(_sectorTileAmount * _sectorTileAmount, Allocator.Persistent);
            integrationFieldSector.Length = _sectorTileAmount * _sectorTileAmount;
            integrationField[pickedSectors[i]] = integrationFieldSector;
            resetHandles.Add(GetResetFieldJob(integrationField[pickedSectors[i]]).Schedule(_sectorTileAmount * _sectorTileAmount, 512));
        }
        JobHandle.CombineDependencies(resetHandles).Complete();
        //prepJob.Schedule(integrationField.Length, 1024).Complete();
        //losJob.Schedule().Complete();
        /*intJob.Schedule().Complete();
        flowfieldJob.Schedule(integrationField.Length, 512).Complete();*/
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
                IntegrationField = integrationField,
                PickedSectors = pickedSectors
            };
        }
        IntFieldResetJob GetResetFieldJob(UnsafeList<IntegrationTile> integrationFieldSector)
        {
            return new IntFieldResetJob(integrationFieldSector);
        }/*
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
    {
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
        }
    }
}