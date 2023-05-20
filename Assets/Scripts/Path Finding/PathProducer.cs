using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
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

    public void ProducePath(Vector3 source, Vector3 destination, int offset)
    {
        CostField pickedCostField = _costFieldProducer.GetCostFieldWithOffset(offset);
        NativeArray<float> portalDistances = new NativeArray<float>(pickedCostField.FieldGraph.PortalNodes.Length, Allocator.Persistent);
        NativeArray<int> connectionIndicies = new NativeArray<int>(pickedCostField.FieldGraph.PortalNodes.Length, Allocator.Persistent);
        NativeList<PortalSequence> portalSequence = new NativeList<PortalSequence>(Allocator.Persistent);
        NativeList<int> pickedSectors = new NativeList<int>(Allocator.Persistent);
        NativeArray<bool> sectorMarks = new NativeArray<bool>(pickedCostField.FieldGraph.SectorNodes.Length, Allocator.Persistent);
        NativeArray<PortalMark> portalMarks = new NativeArray<PortalMark>(pickedCostField.FieldGraph.PortalNodes.Length, Allocator.Persistent);

        ProducedPath = new Path()
        {
            Offset = offset,
            PortalDistances = portalDistances,
            ConnectionIndicies = connectionIndicies,
            PortalSequence = portalSequence,
            SectorMarks = sectorMarks,
            PickedSectors = pickedSectors,
            PortalMarks = portalMarks
        };
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
            SourcePosition = source,
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
        JobHandle traversalJobHandle = traversalJob.Schedule();
        Stopwatch sw = new Stopwatch();
        sw.Start();
        traversalJobHandle.Complete();
        sw.Stop();
        UnityEngine.Debug.Log(sw.Elapsed.TotalMilliseconds);

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
    public void DebugPortalSequence()
    {
        
        Gizmos.color = Color.black;
        float tileSize = _pathfindingManager.TileSize;
        FieldGraph fg = _costFieldProducer.GetCostFieldWithOffset(ProducedPath.Offset).FieldGraph;
        NativeArray<PortalNode> portalNodes = fg.PortalNodes;
        NativeList<PortalSequence> porSeq = ProducedPath.PortalSequence;
        
        UnityEngine.Debug.Log(porSeq.Length);
        for (int i = 0; i < porSeq.Length; i++)
        {
            PortalNode portalNode = portalNodes[porSeq[i].PortalPtr];
            Gizmos.DrawSphere(portalNode.GetPosition(_tileSize), 0.5f);
        }
    }
    public void DebugPickedSectors()
    {
        float yOffset = 0.3f;
        Gizmos.color = Color.black;
        float tileSize = _pathfindingManager.TileSize;
        FieldGraph fg = _costFieldProducer.GetCostFieldWithOffset(ProducedPath.Offset).FieldGraph;
        NativeArray<SectorNode> sectorNodes = fg.SectorNodes;
        NativeArray<int> pickedSectorNodes = ProducedPath.PickedSectors;
        for(int i = 0; i < pickedSectorNodes.Length; i++)
        {
            Index2 index = sectorNodes[pickedSectorNodes[i]].Sector.StartIndex;
            int sectorSize = sectorNodes[pickedSectorNodes[i]].Sector.Size;
            Vector3 botLeft = new Vector3(index.C * tileSize, yOffset, index.R * tileSize);
            Vector3 botRight = new Vector3((index.C + sectorSize) * tileSize, yOffset, index.R * tileSize);
            Vector3 topLeft = new Vector3(index.C * tileSize, yOffset, (index.R + sectorSize) * tileSize);
            Vector3 topRight = new Vector3((index.C + sectorSize) * tileSize, yOffset, (index.R + sectorSize) * tileSize);
            Gizmos.DrawLine(botLeft, topLeft);
            Gizmos.DrawLine(topLeft, topRight);
            Gizmos.DrawLine(topRight, botRight);
            Gizmos.DrawLine(botRight, botLeft);
        }
    }
}