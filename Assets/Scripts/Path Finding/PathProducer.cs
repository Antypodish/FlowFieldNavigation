using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class PathProducer
{
    public Path ProducedPath;

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
    }
    public void Update()
    {
        if(ProducedPath.State == PathState.Dirty)
        {
            NativeArray<Vector3> sources = new NativeArray<Vector3>(ProducedPath.Sources, Allocator.Persistent);
            Vector3 destination = ProducedPath.Destination;
            ProducedPath.Dispose();
            _pathfindingManager.SetDestination(sources, destination);
        }
    }
    public FlowFieldJobPack ProducePath(NativeArray<Vector3> sources, Vector3 destination, int offset)
    {
        Index2 destinationIndex = new Index2(Mathf.FloorToInt(destination.z / _tileSize), Mathf.FloorToInt(destination.x / _tileSize));
        int destionationIndexFlat = destinationIndex.R * _columnAmount + destinationIndex.C;
        CostField pickedCostField = _costFieldProducer.GetCostFieldWithOffset(offset);
        NativeArray<float> portalDistances = new NativeArray<float>(pickedCostField.FieldGraph.PortalNodes.Length, Allocator.Persistent);
        NativeArray<int> connectionIndicies = new NativeArray<int>(pickedCostField.FieldGraph.PortalNodes.Length, Allocator.Persistent);
        NativeList<PortalSequence> portalSequence = new NativeList<PortalSequence>(Allocator.Persistent);
        NativeList<int> pickedSectors = new NativeList<int>(Allocator.Persistent);
        NativeArray<bool> sectorMarks = new NativeArray<bool>(pickedCostField.FieldGraph.SectorNodes.Length, Allocator.Persistent);
        NativeArray<PortalMark> portalMarks = new NativeArray<PortalMark>(pickedCostField.FieldGraph.PortalNodes.Length, Allocator.Persistent);
        NativeArray<IntegrationTile> integrationField = new NativeArray<IntegrationTile>(pickedCostField.Costs.Length, Allocator.Persistent);
        NativeQueue<int> integrationQueue = new NativeQueue<int>(Allocator.Persistent);
        NativeArray<FlowData> flowField = new NativeArray<FlowData>(pickedCostField.Costs.Length, Allocator.Persistent);

        ProducedPath = new Path()
        {
            Sources = sources,
            Destination = destination,
            State = PathState.Clean,
            Offset = offset,
            PortalDistances = portalDistances,
            ConnectionIndicies = connectionIndicies,
            PortalSequence = portalSequence,
            SectorMarks = sectorMarks,
            PickedSectors = pickedSectors,
            PortalMarks = portalMarks,
            IntegrationField = integrationField,
            FlowField = flowField,
        };
        return new FlowFieldJobPack()
        {
            TraversalJob = GetTraversalJob(),
            ResetJob = GetResetFieldJob(),
            PrepJob = GetPreperationJob(),
            IntegrationJob = GetIntegrationJob(),
            FlowFieldJob = GetFlowFieldJob()
        };

        //HELPERS
        FieldGraphTraversalJob GetTraversalJob()
        {
            FieldGraphTraversalJob traversalJob = new FieldGraphTraversalJob()
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
                PickedSectors = pickedSectors
            };
            return traversalJob;
        }
        IntFieldResetJob GetResetFieldJob()
        {
            IntFieldResetJob resetJob = new IntFieldResetJob(integrationField);
            return resetJob;
        }
        IntFieldPrepJob GetPreperationJob()
        {
            IntFieldPrepJob prepJob = new IntFieldPrepJob()
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
            return prepJob;
        }
        IntFieldJob GetIntegrationJob()
        {
            IntFieldJob intJob = new IntFieldJob()
            {
                Costs = pickedCostField.Costs,
                DirectionData = _costFieldProducer.Directions,
                InitialWaveFront = destionationIndexFlat,
                IntegrationField = integrationField,
                IntegrationQueue = integrationQueue
            };
            return intJob;
        }
        FlowFieldJob GetFlowFieldJob()
        {
            FlowFieldJob flowFieldJob = new FlowFieldJob()
            {
                DirectionData = _costFieldProducer.Directions,
                FlowField = flowField,
                IntegrationField = integrationField
            };
            return flowFieldJob;
        }
    }
    public void MarkSectors(NativeList<int> editedSectorIndicies)
    {
        NativeList<int> pickedSectors = ProducedPath.PickedSectors;
        for(int i = 0; i < editedSectorIndicies.Length; i++)
        {
            if (pickedSectors.Contains(editedSectorIndicies[i]))
            {
                ProducedPath.State = PathState.Dirty;
                break;
            }
        }
    }
    public PathDebugger GetPathDebugger()
    {
        return new PathDebugger(_pathfindingManager);
    }
}