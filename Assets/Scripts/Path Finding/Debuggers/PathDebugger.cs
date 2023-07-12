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

    public void DebugPortalTraversalMarks()
    {
        if (_pathProducer == null) { return; }
        Path producedPath = _pathProducer.ProducedPaths.Last();
        if (producedPath == null) { return; }

        float tileSize = _pathfindingManager.TileSize;
        FieldGraph fg = _costFieldProducer.GetCostFieldWithOffset(producedPath.Offset).FieldGraph;
        NativeArray<PortalNode> portalNodes = fg.PortalNodes;
        NativeArray<PortalTraversalData> portalTraversalDataArray = producedPath.PortalTraversalDataArray;
        for(int i = 0; i < portalNodes.Length; i++)
        {
            PortalTraversalData travData = portalTraversalDataArray[i];
            PortalNode node = portalNodes[i];
            if ((travData.mark & PortalTraversalMark.Picked) == PortalTraversalMark.Picked)
            {
                Gizmos.color = Color.blue;
                Vector3 portalPos = node.GetPosition(tileSize);
                Gizmos.DrawSphere(portalPos, 0.25f);
                Handles.Label(portalPos + new Vector3(0,0 , 0.75f), i.ToString());
            }
            else if ((travData.mark & PortalTraversalMark.TargetNeighbour) == PortalTraversalMark.TargetNeighbour)
            {
                Gizmos.color = Color.magenta;
                Vector3 portalPos = node.GetPosition(tileSize);
                Gizmos.DrawSphere(portalPos, 0.25f);
                Handles.Label(portalPos + new Vector3(0, 0, 0.75f), i.ToString());
            }
            else if ((travData.mark & PortalTraversalMark.Considered) == PortalTraversalMark.Considered)
            {
                Gizmos.color = Color.red;
                Vector3 portalPos = node.GetPosition(tileSize);
                Gizmos.DrawSphere(portalPos, 0.25f);
                Handles.Label(portalPos + new Vector3(0, 0, 0.75f), i.ToString());
            }
            else if ((travData.mark & PortalTraversalMark.Included) == PortalTraversalMark.Included)
            {
                Gizmos.color = Color.yellow;
                Vector3 portalPos = node.GetPosition(tileSize);
                Gizmos.DrawSphere(portalPos, 0.25f);
                Handles.Label(portalPos + new Vector3(0, 0, 0.75f), i.ToString());
            }
        }
    }
    public void DebugPortalSequence()
    {
        if (_pathProducer == null) { return; }
        Path producedPath = _pathProducer.ProducedPaths.Last();
        if (producedPath == null) { return; }

        float tileSize = _pathfindingManager.TileSize;
        FieldGraph fg = _costFieldProducer.GetCostFieldWithOffset(producedPath.Offset).FieldGraph;
        NativeArray<PortalNode> portalNodes = fg.PortalNodes;
        NativeList<int> porSeq = producedPath.PortalSequence;
        NativeList<int> portSeqBorders = producedPath.PortalSequenceBorders;
        for(int i = 0; i < portSeqBorders.Length - 1; i++)
        {
            int start = portSeqBorders[i];
            int end = portSeqBorders[i + 1];
            for(int j = start; j < end - 1; j++)
            {
                Gizmos.color = Color.black;
                PortalNode firstportalNode = portalNodes[porSeq[j]];
                PortalNode secondportalNode = portalNodes[porSeq[j + 1]];
                if(firstportalNode.Portal1.Index.R == 0) { continue; }
                Vector3 firstPorPos = firstportalNode.GetPosition(_tileSize);
                Vector3 secondPorPos = secondportalNode.GetPosition(_tileSize);
                Gizmos.DrawLine(firstPorPos, secondPorPos);
            }
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
        UnsafeList<int> sectorMarks = producedPath.SectorToPicked;
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
    public void DebugIntegrationField()
    {
        if (_pathProducer == null) { return; }
        Path producedPath = _pathProducer.ProducedPaths.Last();
        if (producedPath == null) { return; }
        int sectorColAmount = _pathfindingManager.SectorTileAmount;
        int sectorTileAmount = sectorColAmount * sectorColAmount;
        NativeArray<SectorNode> sectorNodes = _costFieldProducer.GetCostFieldWithOffset(producedPath.Offset).FieldGraph.SectorNodes;
        UnsafeList<int> sectorMarks = producedPath.SectorToPicked;
        NativeArray<IntegrationTile> integrationField = producedPath.IntegrationField;
        for (int i = 0; i < sectorMarks.Length; i++)
        {
            if (sectorMarks[i] == 0) { continue; }
            int pickedStartIndex = sectorMarks[i];
            int2 sectorIndex = new int2(sectorNodes[i].Sector.StartIndex.C, sectorNodes[i].Sector.StartIndex.R);
            Vector3 sectorIndexPos = new Vector3(sectorIndex.x * _tileSize, 0f, sectorIndex.y * _tileSize);
            for (int j = pickedStartIndex; j < pickedStartIndex + sectorTileAmount; j++)
            {
                int local1d = (j - 1) % sectorTileAmount;
                int2 local2d = new int2(local1d % sectorColAmount, local1d / sectorColAmount);
                Vector3 localIndexPos = new Vector3(local2d.x * _tileSize, 0f, local2d.y * _tileSize);
                Vector3 debugPos = localIndexPos + sectorIndexPos + new Vector3(_tileSize / 2, 0.02f, _tileSize / 2);
                float cost = integrationField[j].Cost;
                if (cost != float.MaxValue)
                {
                    Handles.Label(debugPos, cost.ToString());
                }
            }
            
        }
    }
    public void LOSPassDebug()
    {
        if (_pathProducer == null) { return; }
        Path producedPath = _pathProducer.ProducedPaths.Last();
        if (producedPath == null) { return; }

        string los = "los";
        Gizmos.color = Color.white;
        UnsafeList<int> sectorMarks = producedPath.SectorToPicked;
        NativeArray<SectorNode> sectorNodes = _costFieldProducer.GetCostFieldWithOffset(producedPath.Offset).FieldGraph.SectorNodes;
        NativeArray<IntegrationTile> integrationField = producedPath.IntegrationField;
        int sectorColAmount = _pathfindingManager.SectorTileAmount;
        int sectorTileAmount = sectorColAmount * sectorColAmount;
        for (int i = 0; i < sectorMarks.Length; i++)
        {
            if (sectorMarks[i] == 0) { continue; }
            int pickedStartIndex = sectorMarks[i];
            for (int j = pickedStartIndex; j < pickedStartIndex + sectorTileAmount; j++)
            {
                if (integrationField[j].Mark == IntegrationMark.LOSPass)
                {
                    int2 sectorIndex = new int2(sectorNodes[i].Sector.StartIndex.C, sectorNodes[i].Sector.StartIndex.R);
                    Vector3 sectorIndexPos = new Vector3(sectorIndex.x * _tileSize, 0f, sectorIndex.y * _tileSize);
                    int local1d = (j - 1) % sectorTileAmount;
                    int2 local2d = new int2(local1d % sectorColAmount, local1d / sectorColAmount);
                    Vector3 localIndexPos = new Vector3(local2d.x * _tileSize, 0f, local2d.y * _tileSize);
                    Vector3 debugPos = localIndexPos + sectorIndexPos + new Vector3(_tileSize / 2, 0.02f, _tileSize / 2);
                    Handles.Label(debugPos, los);
                }
            }
        }

    }
    public void LOSBlockDebug()
    {
        if (_pathProducer == null) { return; }
        Path producedPath = _pathProducer.ProducedPaths.Last();
        if (producedPath == null) { return; }

        Gizmos.color = Color.white;
        UnsafeList<int> sectorMarks = producedPath.SectorToPicked;
        NativeArray<SectorNode> sectorNodes = _costFieldProducer.GetCostFieldWithOffset(producedPath.Offset).FieldGraph.SectorNodes;
        NativeArray<IntegrationTile> integrationField = producedPath.IntegrationField;
        int sectorColAmount = _pathfindingManager.SectorTileAmount;
        int sectorTileAmount = sectorColAmount * sectorColAmount;
        for (int i = 0; i < sectorMarks.Length; i++)
        {
            if (sectorMarks[i] == 0) { continue; }
            int pickedStartIndex = sectorMarks[i];
            for (int j = pickedStartIndex; j < pickedStartIndex + sectorTileAmount; j++)
            {
                if (integrationField[j].Mark == IntegrationMark.LOSBlock)
                {
                    int2 sectorIndex = new int2(sectorNodes[i].Sector.StartIndex.C, sectorNodes[i].Sector.StartIndex.R);
                    Vector3 sectorIndexPos = new Vector3(sectorIndex.x * _tileSize, 0f, sectorIndex.y * _tileSize);
                    int local1d = (j - 1) % sectorTileAmount;
                    int2 local2d = new int2(local1d % sectorColAmount, local1d / sectorColAmount);
                    Vector3 localIndexPos = new Vector3(local2d.x * _tileSize, 0f, local2d.y * _tileSize);
                    Vector3 debugPos = localIndexPos + sectorIndexPos + new Vector3(_tileSize / 2, 0.02f, _tileSize / 2);
                    Gizmos.DrawCube(debugPos, new Vector3(0.3f, 0.3f, 0.3f));
                }
            } 
        }
    }
    public void DebugFlowField()
    {
        if (_pathProducer == null) { return; }
        Path producedPath = _pathProducer.ProducedPaths.Last();
        if (producedPath == null) { return; }

        float yOffset = 0.2f;
        Gizmos.color = Color.black;
        UnsafeList<int> sectorMarks = producedPath.SectorToPicked;
        NativeArray<SectorNode> sectorNodes = _costFieldProducer.GetCostFieldWithOffset(producedPath.Offset).FieldGraph.SectorNodes;
        UnsafeList<FlowData> flowField = producedPath.FlowField;
        int sectorColAmount = _pathfindingManager.SectorTileAmount;
        int sectorTileAmount = sectorColAmount * sectorColAmount;
        for (int i = 0; i < sectorMarks.Length; i++)
        {
            if (sectorMarks[i] == 0) { continue; }
            int pickedStartIndex = sectorMarks[i];
            for (int j = pickedStartIndex; j < pickedStartIndex + sectorTileAmount; j++)
            {
                int2 sectorIndex = new int2(sectorNodes[i].Sector.StartIndex.C, sectorNodes[i].Sector.StartIndex.R);
                Vector3 sectorIndexPos = new Vector3(sectorIndex.x * _tileSize, 0f, sectorIndex.y * _tileSize);
                int local1d = (j - 1) % sectorTileAmount;
                int2 local2d = new int2(local1d % sectorColAmount, local1d / sectorColAmount);
                Vector3 localIndexPos = new Vector3(local2d.x * _tileSize, 0f, local2d.y * _tileSize);
                Vector3 debugPos = localIndexPos + sectorIndexPos + new Vector3(_tileSize / 2, 0.02f, _tileSize / 2);
                if (flowField[j] != FlowData.None)
                {
                    DrawSquare(debugPos, 0.2f);
                }
                if (flowField[j] != FlowData.LOS)
                {
                    DrawFlow(flowField[j], debugPos);
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