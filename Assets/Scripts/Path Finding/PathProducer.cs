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

    public void ProducePath(NativeArray<Vector3> sources, Vector3 destination, int offset)
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
        
        //traverse portal graph
        FieldGraphTraversalJob traversalJob = GetTraversalJob();
        JobHandle traversalJobHandle = traversalJob.Schedule();
        traversalJobHandle.Complete();

        //reset field
        IntFieldResetJob resetJob = GetResetFieldJob();
        JobHandle resetJobHandle = resetJob.Schedule(integrationField.Length, 512);
        resetJobHandle.Complete();

        //prepareField
        IntFieldPrepJob prepJob = GetPreperationJob();
        JobHandle prepHandle = prepJob.Schedule(_rowAmount * _rowAmount, 1024);
        prepHandle.Complete();

        //BFS
        IntFieldJob intJob = GetIntegrationJob();
        JobHandle intJobHandle = intJob.Schedule();
        intJobHandle.Complete();

        //FlowField
        FlowFieldJob flowFieldJob = GetFlowFieldJob();
        JobHandle flowFieldJobHandle = flowFieldJob.Schedule(flowField.Length, 512);
        flowFieldJobHandle.Complete();

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
    public void DebugIntegrationField(NativeArray<Vector3> tilePositions)
    {
        float yOffset = 0.2f;
        NativeArray<IntegrationTile> integrationField = ProducedPath.IntegrationField;
        for(int i = 0; i < integrationField.Length; i++)
        {
            DebugCosts(i);
        }
        void DebugCosts(int index)
        {
            int cost = integrationField[index].Cost;
            if (cost == int.MaxValue)
            {
                return;
            }
            Handles.Label(tilePositions[index], cost.ToString());
        }
        void DebugMarks(int index)
        {
            IntegrationMark mark = integrationField[index].Mark;
            if (mark == IntegrationMark.None)
            {
                Handles.Label(tilePositions[index], "N");
            }

        }
    }
    public void DebugFlowField(NativeArray<Vector3> tilePositions)
    {
        float yOffset = 0.1f;
        Gizmos.color = Color.black;
        float tileSize = _tileSize;
        NativeArray<FlowData> flowfield = ProducedPath.FlowField;
        for(int i = 0; i < flowfield.Length; i++)
        {
            if (flowfield[i] == FlowData.None) { continue; }
            DrawSquare(tilePositions[i], 0.125f);
            DrawFlow(flowfield[i], tilePositions[i]);
        }

        void DrawSquare(Vector3 pos, float size)
        {
            Vector3 botLeft = new Vector3(pos.x - size / 2, yOffset, pos.z - size / 2);
            Vector3 botRight = new Vector3(pos.x + size / 2, yOffset, pos.z - size / 2);
            Vector3 topLeft = new Vector3(pos.x - size / 2, yOffset, pos.z + size / 2);
            Vector3 topRight = new Vector3(pos.x + size / 2, yOffset, pos.z + size / 2);

            Gizmos.DrawLine(topRight, botRight);
            Gizmos.DrawLine(botRight, botLeft);
            Gizmos.DrawLine(botLeft, topLeft);
            Gizmos.DrawLine(topLeft, topRight);
        }
        void DrawFlow(FlowData flow, Vector3 pos)
        {
            pos = pos + new Vector3(0, yOffset, 0);

            if (flow == FlowData.N)
            {
                Vector3 dir = pos + new Vector3(0, yOffset, 0.4f);
                Gizmos.DrawLine(pos, dir);
            }
            else if (flow == FlowData.NE)
            {
                Vector3 dir = pos + new Vector3(0.4f, yOffset, 0.4f);
                Gizmos.DrawLine(pos, dir);
            }
            else if (flow == FlowData.E)
            {
                Vector3 dir = pos + new Vector3(0.4f, yOffset, 0);
                Gizmos.DrawLine(pos, dir);
            }
            else if (flow == FlowData.SE)
            {
                Vector3 dir = pos + new Vector3(0.4f, yOffset, -0.4f);
                Gizmos.DrawLine(pos, dir);
            }
            else if (flow == FlowData.S)
            {
                Vector3 dir = pos + new Vector3(0, yOffset, -0.4f);
                Gizmos.DrawLine(pos, dir);
            }
            else if (flow == FlowData.SW)
            {
                Vector3 dir = pos + new Vector3(-0.4f, yOffset, -0.4f);
                Gizmos.DrawLine(pos, dir);
            }
            else if (flow == FlowData.W)
            {
                Vector3 dir = pos + new Vector3(-0.4f, yOffset, 0);
                Gizmos.DrawLine(pos, dir);
            }
            else if (flow == FlowData.NW)
            {
                Vector3 dir = pos + new Vector3(-0.4f, yOffset, 0.4f);
                Gizmos.DrawLine(pos, dir);
            }
        }
    }
    
}