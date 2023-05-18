using System.Diagnostics;
using Unity.Collections;
using Unity.Jobs;
using UnityEditor;
using UnityEngine;

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

    public void ProducePath(Vector3 destination, int offset)
    {
        CostField pickedCostField = _costFieldProducer.GetCostFieldWithOffset(offset);
        NativeArray<float> portalDistances = new NativeArray<float>(pickedCostField.FieldGraph.PortalNodes.Length, Allocator.Persistent);
        NativeArray<int> connectionIndicies = new NativeArray<int>(pickedCostField.FieldGraph.PortalNodes.Length, Allocator.Persistent);

        ProducedPath = new Path()
        {
            Offset = offset,
            PortalDistances = portalDistances,
            ConnectionIndicies = connectionIndicies
        };
        FieldGraphTraversalJob traversalJob = new FieldGraphTraversalJob()
        {
            FieldColAmount = _columnAmount,
            PortalNodes = pickedCostField.FieldGraph.PortalNodes,
            SecToWinPtrs = pickedCostField.FieldGraph.SecToWinPtrs,
            WindowNodes = pickedCostField.FieldGraph.WindowNodes,
            FieldRowAmount = _rowAmount,
            FieldTileSize = _tileSize,
            TargetPosition = destination,
            PorPtrs = pickedCostField.FieldGraph.PorToPorPtrs,
            SectorNodes = pickedCostField.FieldGraph.SectorNodes,
            Costs = pickedCostField.Costs,
            SectorTileAmount = _sectorTileAmount,
            SectorMatrixColAmount = _columnAmount / _sectorTileAmount,
            LocalDirections = _costFieldProducer.LocalDirections,
            ConnectionIndicies = connectionIndicies,
            PortalDistances = portalDistances,
        };
        JobHandle traversalJobHandle = traversalJob.Schedule();
        traversalJobHandle.Complete();

    }
    public void DebugBFS()
    {
        Gizmos.color = Color.black;

        float tileSize = _pathfindingManager.TileSize;
        FieldGraph fg = _costFieldProducer.GetCostFieldWithOffset(ProducedPath.Offset).FieldGraph;
        NativeArray<PortalNode> portalNodes = fg.PortalNodes;
        NativeArray<int> connections = ProducedPath.ConnectionIndicies;
        NativeArray<float> distances = ProducedPath.PortalDistances;
        for (int i = 0; i < portalNodes.Length; i++)
        {
            PortalNode startNode = portalNodes[i];
            if (startNode.Portal1.Index.R == 0) { continue; }
            PortalNode endNode = portalNodes[connections[i]];
            Vector3 start = startNode.GetPosition(tileSize) + new Vector3(0, 0.05f, 0);
            Vector3 end = endNode.GetPosition(tileSize) + new Vector3(0, 0.05f, 0);
            float distance = Vector3.Distance(start, end);
            end = Vector3.MoveTowards(start, end, distance - 0.3f);
            float cost = distances[i];
            Gizmos.DrawLine(start, end);
            Handles.Label(start + new Vector3(0, 0, 0.5f), cost.ToString());
        }

        
    }
}