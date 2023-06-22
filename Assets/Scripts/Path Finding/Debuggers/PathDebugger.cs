#if (UNITY_EDITOR)

using System;
using System.Linq;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Analytics;

public class PathDebugger
{
    PathProducer _pathProducer;
    PathfindingManager _pathfindingManager;
    CostFieldProducer _costFieldProducer;
    float _tileSize;

    public PathDebugger(PathfindingManager pathfindingManager)
    {
        _pathProducer = pathfindingManager.PathProducer;
        _pathfindingManager = pathfindingManager;
        _costFieldProducer = pathfindingManager.CostFieldProducer;
        _tileSize = pathfindingManager.TileSize;
    }

    public void DebugBFS()
    {
        Gizmos.color = Color.black;
        if (_pathProducer == null) { return; }
        Path producedPath = _pathProducer.ProducedPaths.Last();
        if (producedPath == null) { return; }

        float tileSize = _pathfindingManager.TileSize;
        FieldGraph fg = _costFieldProducer.GetCostFieldWithOffset(producedPath.Offset).FieldGraph;
        NativeArray<PortalNode> portalNodes = fg.PortalNodes;
        NativeArray<int> connections = producedPath.ConnectionIndicies;
        NativeArray<float> distances = producedPath.PortalDistances;
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
        Gizmos.color = Color.red;
        if (_pathProducer == null) { return; }
        Path producedPath = _pathProducer.ProducedPaths.Last();
        if (producedPath == null) { return; }

        float tileSize = _pathfindingManager.TileSize;
        FieldGraph fg = _costFieldProducer.GetCostFieldWithOffset(producedPath.Offset).FieldGraph;
        NativeArray<PortalNode> portalNodes = fg.PortalNodes;
        NativeList<PortalSequence> porSeq = producedPath.PortalSequence;
        for (int i = 0; i < porSeq.Length; i++)
        {
            PortalNode portalNode = portalNodes[porSeq[i].PortalPtr];
            Gizmos.DrawSphere(portalNode.GetPosition(_tileSize), 0.25f);
        }
    }
    public void DebugPickedSectors()
    {
        if (_pathProducer == null) { return; }
        Path producedPath = _pathProducer.ProducedPaths.Last();
        if (producedPath == null) { return; }

        float yOffset = 0.3f;
        Gizmos.color = Color.black;
        float tileSize = _pathfindingManager.TileSize;
        FieldGraph fg = _costFieldProducer.GetCostFieldWithOffset(producedPath.Offset).FieldGraph;
        NativeArray<SectorNode> sectorNodes = fg.SectorNodes;
        NativeArray<int> sectorMarks = producedPath.SectorMarks;
        for (int i = 0; i < sectorMarks.Length; i++)
        {
            if (sectorMarks[i] == 0) { continue; }
            Index2 index = sectorNodes[i].Sector.StartIndex;
            int sectorSize = sectorNodes[i].Sector.Size;
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
        if (_pathProducer == null) { return; }
        Path producedPath = _pathProducer.ProducedPaths.Last();
        if (producedPath == null) { return; }
        int fieldColAmount = _pathfindingManager.ColumnAmount / _pathfindingManager.SectorTileAmount;
        NativeArray<SectorNode> sectorNodes = _costFieldProducer.GetCostFieldWithOffset(producedPath.Offset).FieldGraph.SectorNodes;
        NativeArray<int> sectorMarks = producedPath.SectorMarks;
        NativeArray<IntegrationFieldSector> integrationField = producedPath.IntegrationField;
        int sectorTileAmount = _pathfindingManager.SectorTileAmount;
        for (int i = 0; i < sectorMarks.Length; i++)
        {
            if (sectorMarks[i] == 0) { continue; }
            UnsafeList<IntegrationTile> sector = integrationField[sectorMarks[i]].integrationSector;
            int2 sectorIndex = new int2(sectorNodes[i].Sector.StartIndex.C, sectorNodes[i].Sector.StartIndex.R);
            Vector3 sectorIndexPos = new Vector3(sectorIndex.x * _tileSize, 0f, sectorIndex.y * _tileSize);
            for(int j = 0; j < sector.Length; j++)
            {
                int2 localIndex = new int2(j % sectorTileAmount, j / sectorTileAmount);
                Vector3 localIndexPos = new Vector3(localIndex.x * _tileSize, 0f, localIndex.y * _tileSize);
                Vector3 debugPos = localIndexPos + sectorIndexPos + new Vector3(_tileSize / 2, 0.02f, _tileSize / 2);
                float cost = sector[j].Cost;
                if (cost == float.MaxValue)
                {
                    continue;
                }
                else
                {
                    Handles.Label(debugPos, cost.ToString());
                }
            }
            
        }
    }
    public void LOSPassDebug(NativeArray<Vector3> tilePositions)
    {
        if (_pathProducer == null) { return; }
        Path producedPath = _pathProducer.ProducedPaths.Last();
        if (producedPath == null) { return; }

        string los = "los";
        Gizmos.color = Color.white;
        NativeArray<int> sectorMarks = producedPath.SectorMarks;
        NativeArray<SectorNode> sectorNodes = _costFieldProducer.GetCostFieldWithOffset(producedPath.Offset).FieldGraph.SectorNodes;
        NativeList<IntegrationFieldSector> integrationField = producedPath.IntegrationField;
        int sectorTileAmount = _pathfindingManager.SectorTileAmount;
        for (int i = 0; i < sectorMarks.Length; i++)
        {
            if (sectorMarks[i] == 0) { continue; }
            UnsafeList<IntegrationTile> integrationSector = integrationField[sectorMarks[i]].integrationSector;
            for (int j = 0; j < integrationSector.Length; j++)
            {
                if (integrationSector[j].Mark == IntegrationMark.LOSPass)
                {
                    int2 sectorIndex = new int2(sectorNodes[i].Sector.StartIndex.C, sectorNodes[i].Sector.StartIndex.R);
                    Vector3 sectorIndexPos = new Vector3(sectorIndex.x * _tileSize, 0f, sectorIndex.y * _tileSize);
                    int2 localIndex = new int2(j % sectorTileAmount, j / sectorTileAmount);
                    Vector3 localIndexPos = new Vector3(localIndex.x * _tileSize, 0f, localIndex.y * _tileSize);
                    Vector3 debugPos = localIndexPos + sectorIndexPos + new Vector3(_tileSize / 2, 0.02f, _tileSize / 2);
                    Handles.Label(debugPos, los);
                }
            }
        }

    }
    public void LOSBlockDebug(NativeArray<Vector3> tilePositions)
    {
        if (_pathProducer == null) { return; }
        Path producedPath = _pathProducer.ProducedPaths.Last();
        if (producedPath == null) { return; }

        Gizmos.color = Color.white;
        NativeArray<int> sectorMarks = producedPath.SectorMarks;
        NativeArray<SectorNode> sectorNodes = _costFieldProducer.GetCostFieldWithOffset(producedPath.Offset).FieldGraph.SectorNodes;
        NativeList<IntegrationFieldSector> integrationField = producedPath.IntegrationField;
        int sectorTileAmount = _pathfindingManager.SectorTileAmount;
        for (int i = 0; i < sectorMarks.Length; i++)
        {
            if (sectorMarks[i] == 0) { continue; }
            UnsafeList<IntegrationTile> integrationSector = integrationField[sectorMarks[i]].integrationSector;
            for(int j = 0; j < integrationSector.Length; j++)
            {
                if (integrationSector[j].Mark == IntegrationMark.LOSBlock)
                {
                    int2 sectorIndex = new int2(sectorNodes[i].Sector.StartIndex.C, sectorNodes[i].Sector.StartIndex.R);
                    Vector3 sectorIndexPos = new Vector3(sectorIndex.x * _tileSize, 0f, sectorIndex.y * _tileSize);
                    int2 localIndex = new int2(j % sectorTileAmount, j / sectorTileAmount);
                    Vector3 localIndexPos = new Vector3(localIndex.x * _tileSize, 0f, localIndex.y * _tileSize);
                    Vector3 debugPos = localIndexPos + sectorIndexPos + new Vector3(_tileSize / 2, 0.02f, _tileSize / 2);
                    Gizmos.DrawCube(debugPos, new Vector3(0.3f, 0.3f, 0.3f));
                }
            } 
        }
    }
    public void DebugFlowField(NativeArray<Vector3> tilePositions)
    {
        if (_pathProducer == null) { return; }
        Path producedPath = _pathProducer.ProducedPaths.Last();
        if (producedPath == null) { return; }

        float yOffset = 0.2f;
        Gizmos.color = Color.black;
        NativeArray<int> sectorMarks = producedPath.SectorMarks;
        NativeArray<SectorNode> sectorNodes = _costFieldProducer.GetCostFieldWithOffset(producedPath.Offset).FieldGraph.SectorNodes;
        NativeList<FlowFieldSector> flowField = producedPath.FlowField;
        int sectorTileAmount = _pathfindingManager.SectorTileAmount;
        for (int i = 0; i < sectorMarks.Length; i++)
        {
            if (sectorMarks[i] == 0) { continue; }
            UnsafeList<FlowData> flowSector = flowField[sectorMarks[i]].flowfieldSector;
            for (int j = 0; j < flowSector.Length; j++)
            {
                int2 sectorIndex = new int2(sectorNodes[i].Sector.StartIndex.C, sectorNodes[i].Sector.StartIndex.R);
                Vector3 sectorIndexPos = new Vector3(sectorIndex.x * _tileSize, 0f, sectorIndex.y * _tileSize);
                int2 localIndex = new int2(j % sectorTileAmount, j / sectorTileAmount);
                Vector3 localIndexPos = new Vector3(localIndex.x * _tileSize, 0f, localIndex.y * _tileSize);
                Vector3 debugPos = localIndexPos + sectorIndexPos + new Vector3(_tileSize / 2, 0.02f, _tileSize / 2);
                if (flowSector[j] != FlowData.None)
                {
                    DrawSquare(debugPos, 0.2f);
                }
                if (flowSector[j] != FlowData.LOS)
                {
                    DrawFlow(flowSector[j], debugPos);
                }
            }
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
                Vector3 dir = pos + new Vector3(0, 0, 0.4f);
                Gizmos.DrawLine(pos, dir);
            }
            else if (flow == FlowData.NE)
            {
                Vector3 dir = pos + new Vector3(0.4f, 0, 0.4f);
                Gizmos.DrawLine(pos, dir);
            }
            else if (flow == FlowData.E)
            {
                Vector3 dir = pos + new Vector3(0.4f, 0, 0);
                Gizmos.DrawLine(pos, dir);
            }
            else if (flow == FlowData.SE)
            {
                Vector3 dir = pos + new Vector3(0.4f, 0, -0.4f);
                Gizmos.DrawLine(pos, dir);
            }
            else if (flow == FlowData.S)
            {
                Vector3 dir = pos + new Vector3(0, 0, -0.4f);
                Gizmos.DrawLine(pos, dir);
            }
            else if (flow == FlowData.SW)
            {
                Vector3 dir = pos + new Vector3(-0.4f, 0, -0.4f);
                Gizmos.DrawLine(pos, dir);
            }
            else if (flow == FlowData.W)
            {
                Vector3 dir = pos + new Vector3(-0.4f, 0, 0);
                Gizmos.DrawLine(pos, dir);
            }
            else if (flow == FlowData.NW)
            {
                Vector3 dir = pos + new Vector3(-0.4f, 0, 0.4f);
                Gizmos.DrawLine(pos, dir);
            }
        }
    }
}
#endif