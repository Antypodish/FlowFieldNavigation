#if (UNITY_EDITOR)

using System;
using System.Linq;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Analytics;
using System.Diagnostics;
public class EditorPathDebugger
{
    PathProducer _pathProducer;
    PathfindingManager _pathfindingManager;
    FieldProducer _fieldProducer;
    float _tileSize;

    public EditorPathDebugger(PathfindingManager pathfindingManager)
    {
        _pathProducer = pathfindingManager.PathProducer;
        _pathfindingManager = pathfindingManager;
        _fieldProducer = pathfindingManager.FieldProducer;
        _tileSize = pathfindingManager.TileSize;
    }
    public void DebugActiveWaveFronts(FlowFieldAgent agent)
    {
        if (_pathProducer == null) { return; }
        if (_pathProducer.ProducedPaths.Count == 0) { return; }
        Path producedPath = agent.GetPath();
        if (!producedPath.IsCalculated) { return; }


        float tileSize = _pathfindingManager.TileSize;
        int sectorColAmount = FlowFieldUtilities.SectorColAmount;

        FieldGraph fg = _fieldProducer.GetFieldGraphWithOffset(producedPath.Offset);
        NativeArray<UnsafeList<ActiveWaveFront>> waveFronts = producedPath.ActiveWaveFrontList;
        NativeArray<int> pickedToSector = producedPath.PickedToSector;
        Gizmos.color = Color.red;
        for (int i = 0; i < pickedToSector.Length; i++)
        {
            int sectorIndex = pickedToSector[i];
            int2 sector2d = FlowFieldUtilities.To2D(sectorIndex, FlowFieldUtilities.SectorMatrixColAmount);
            UnsafeList<ActiveWaveFront> fronts = waveFronts[i];
            for (int j = 0; j < fronts.Length; j++)
            {
                ActiveWaveFront front = fronts[j];
                int2 local2d = FlowFieldUtilities.To2D(front.LocalIndex, sectorColAmount);
                int2 general2d = FlowFieldUtilities.GetGeneral2d(local2d, sector2d, sectorColAmount, FlowFieldUtilities.FieldColAmount);
                float2 pos = FlowFieldUtilities.IndexToPos(general2d, tileSize);
                Vector3 pos3 = new Vector3(pos.x, 0f, pos.y);
                Gizmos.DrawCube(pos3, new Vector3(0.6f, 0.6f, 0.6f));
            }            
        }
    }
    public void DebugPortalTraversalMarks(FlowFieldAgent agent)
    {
        if (_pathProducer == null) { return; }
        if(_pathProducer.ProducedPaths.Count == 0) { return; }
        Path producedPath = agent.GetPath();
        if (!producedPath.IsCalculated) { return; }

        float tileSize = _pathfindingManager.TileSize;
        FieldGraph fg = _fieldProducer.GetFieldGraphWithOffset(producedPath.Offset);
        UnsafeList<PortalNode> portalNodes = fg.PortalNodes;
        NativeArray<PortalTraversalData> portalTraversalDataArray = producedPath.PortalTraversalDataArray;

        for (int i = 0; i < portalNodes.Length; i++)
        {
            PortalTraversalData travData = portalTraversalDataArray[i];
            PortalNode node = portalNodes[i];
            if (travData.HasMark(PortalTraversalMark.DijkstraPicked))
            {
                Gizmos.color = Color.white;
                Vector3 portalPos = node.GetPosition(tileSize);
                Handles.Label(portalPos + new Vector3(0, 0, 0.75f), "d: " + travData.DistanceFromTarget.ToString());
                Gizmos.DrawSphere(portalPos, 0.25f);
            }
            else if (travData.HasMark(PortalTraversalMark.Reduced))
            {
                Gizmos.color = Color.black;
                Vector3 portalPos = node.GetPosition(tileSize);
                Handles.Label(portalPos + new Vector3(0, 0, 0.75f), "d: " + travData.DistanceFromTarget.ToString());
                Gizmos.DrawSphere(portalPos, 0.25f);
            }
        }
    }
    public void DebugTargetNeighbourPortals(FlowFieldAgent agent)
    {
        if (_pathProducer == null) { return; }
        if(_pathProducer.ProducedPaths.Count == 0) { return; }
        Path producedPath = agent.GetPath();
        if (!producedPath.IsCalculated) { return; }

        float tileSize = _pathfindingManager.TileSize;
        FieldGraph fg = _fieldProducer.GetFieldGraphWithOffset(producedPath.Offset);
        UnsafeList<PortalNode> portalNodes = fg.PortalNodes;
        NativeArray<PortalTraversalData> portalTraversalDataArray = producedPath.PortalTraversalDataArray;

        for (int i = 0; i < portalNodes.Length; i++)
        {
            PortalTraversalData travData = portalTraversalDataArray[i];
            PortalNode node = portalNodes[i];
            if (travData.HasMark(PortalTraversalMark.TargetNeighbour))
            {
                Gizmos.color = Color.magenta;
                Vector3 portalPos = node.GetPosition(tileSize);
                Handles.Label(portalPos + new Vector3(0, 0, 0.75f), "d: " + travData.DistanceFromTarget.ToString());
                Gizmos.DrawSphere(portalPos, 0.25f);
            }
        }
    }
    public void DebugPortalSequence(FlowFieldAgent agent)
    {
        if (_pathProducer == null) { return; }
        if (_pathProducer.ProducedPaths.Count == 0) { return; }
        Path producedPath = agent.GetPath();
        if (!producedPath.IsCalculated) { return; }
        
        float tileSize = _pathfindingManager.TileSize;
        FieldGraph fg = _fieldProducer.GetFieldGraphWithOffset(producedPath.Offset);
        UnsafeList<PortalNode> portalNodes = fg.PortalNodes;
        NativeList<ActivePortal> porSeq = producedPath.PortalSequence;
        NativeList<int> portSeqBorders = producedPath.PortalSequenceBorders;
        Gizmos.color = Color.white;

        if (porSeq.Length == 0) { return; }

        for (int i = 0; i < portSeqBorders.Length - 1; i++)
        {
            int start = portSeqBorders[i];
            int curNodeIndex = start;
            int nextNodeIndex = porSeq[start].NextIndex;
            while (nextNodeIndex != -1)
            {
                int curPortalIndex = porSeq[curNodeIndex].Index;
                int nextPortalIndex = porSeq[nextNodeIndex].Index;

                //DRAW
                PortalNode firstportalNode = portalNodes[curPortalIndex];
                PortalNode secondportalNode = portalNodes[nextPortalIndex];
                if (firstportalNode.Portal1.Index.R == 0) { continue; }
                Vector3 secondPorPos = secondportalNode.GetPosition(_tileSize);
                Vector3 firstPorPos = firstportalNode.GetPosition(_tileSize);

                Vector3 relativeSecond = secondPorPos - firstPorPos;
                Vector2 relativeSecond2d = new Vector2(relativeSecond.x, relativeSecond.z);
                relativeSecond2d = relativeSecond2d.normalized;
                Vector2 perpLeft = new Vector2(-relativeSecond2d.y, relativeSecond2d.x);
                Vector2 perpRight = new Vector2(relativeSecond2d.y, -relativeSecond2d.x);

                Vector2 rightArrow = (perpRight - relativeSecond2d).normalized;
                Vector2 leftArrow = (perpLeft - relativeSecond2d).normalized;

                Vector3 rightArrow3 = new Vector3(rightArrow.x, secondPorPos.y, rightArrow.y);
                Vector3 leftArrow3 = new Vector3(leftArrow.x, secondPorPos.y, leftArrow.y);
                Gizmos.DrawLine(firstPorPos, secondPorPos);
                Gizmos.DrawLine(secondPorPos, secondPorPos + rightArrow3);
                Gizmos.DrawLine(secondPorPos, secondPorPos + leftArrow3);

                curNodeIndex = nextNodeIndex;
                nextNodeIndex = porSeq[nextNodeIndex].NextIndex;
            }
        }
    }
    public void DebugPickedSectors(FlowFieldAgent agent)
    {
        if (_pathProducer == null) { return; }
        if (_pathProducer.ProducedPaths.Count == 0) { return; }
        Path producedPath = agent.GetPath();
        if (!producedPath.IsCalculated) { return; }

        float yOffset = 0.3f;
        Gizmos.color = Color.black;
        float tileSize = _pathfindingManager.TileSize;
        FieldGraph fg = _fieldProducer.GetFieldGraphWithOffset(producedPath.Offset);
        UnsafeList<SectorNode> sectorNodes = fg.SectorNodes;
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
    public void DebugIntegrationField(FlowFieldAgent agent)
    {
        if (_pathProducer == null) { return; }
        if (_pathProducer.ProducedPaths.Count == 0) { return; }
        Path producedPath = agent.GetPath();
        if (!producedPath.IsCalculated) { return; }
        int sectorColAmount = _pathfindingManager.SectorColAmount;
        int sectorTileAmount = sectorColAmount * sectorColAmount;
        UnsafeList<SectorNode> sectorNodes = _fieldProducer.GetFieldGraphWithOffset(producedPath.Offset).SectorNodes;
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
    public void LOSBlockDebug(FlowFieldAgent agent)
    {
        if (_pathProducer == null) { return; }
        if (_pathProducer.ProducedPaths.Count == 0) { return; }
        Path producedPath = agent.GetPath();
        if (!producedPath.IsCalculated) { return; }

        UnsafeList<int> sectorMarks = producedPath.SectorToPicked;
        UnsafeList<SectorNode> sectorNodes = _fieldProducer.GetFieldGraphWithOffset(producedPath.Offset).SectorNodes;
        NativeArray<IntegrationTile> integrationField = producedPath.IntegrationField;
        int sectorColAmount = _pathfindingManager.SectorColAmount;
        int sectorTileAmount = sectorColAmount * sectorColAmount;
        for (int i = 0; i < sectorMarks.Length; i++)
        {
            if (sectorMarks[i] == 0) { continue; }
            int pickedStartIndex = sectorMarks[i];
            for (int j = pickedStartIndex; j < pickedStartIndex + sectorTileAmount; j++)
            {
                if ((integrationField[j].Mark & IntegrationMark.LOSBlock) == IntegrationMark.LOSBlock)
                {
                    Gizmos.color = Color.white;
                    int2 sectorIndex = new int2(sectorNodes[i].Sector.StartIndex.C, sectorNodes[i].Sector.StartIndex.R);
                    Vector3 sectorIndexPos = new Vector3(sectorIndex.x * _tileSize, 0f, sectorIndex.y * _tileSize);
                    int local1d = (j - 1) % sectorTileAmount;
                    int2 local2d = new int2(local1d % sectorColAmount, local1d / sectorColAmount);
                    Vector3 localIndexPos = new Vector3(local2d.x * _tileSize, 0f, local2d.y * _tileSize);
                    Vector3 debugPos = localIndexPos + sectorIndexPos + new Vector3(_tileSize / 2, 0.02f, _tileSize / 2);
                    Gizmos.DrawCube(debugPos, new Vector3(0.3f, 0.3f, 0.3f));
                }
                else if ((integrationField[j].Mark & IntegrationMark.LOSC) == IntegrationMark.LOSC)
                {
                    Gizmos.color = Color.black;
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
    public void DebugFlowField(FlowFieldAgent agent)
    {
        if (_pathProducer == null) { return; }
        if (_pathProducer.ProducedPaths.Count == 0) { return; }
        Path producedPath = agent.GetPath();
        if (!producedPath.IsCalculated) { return; }

        float yOffset = 0.2f;
        UnsafeList<int> sectorMarks = producedPath.SectorToPicked;
        UnsafeList<SectorNode> sectorNodes = _fieldProducer.GetFieldGraphWithOffset(producedPath.Offset).SectorNodes;
        UnsafeList<FlowData> flowField = producedPath.FlowField;
        UnsafeLOSBitmap losmap = producedPath.LOSMap;
        NativeArray<IntegrationTile> integrationField = producedPath.IntegrationField;
        int sectorColAmount = _pathfindingManager.SectorColAmount;
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
                if(j >= flowField.Length) { continue; }
                if (!flowField[j].IsValid()) { continue; }
                SetColor(j);
                DrawSquare(debugPos, 0.2f);
                DrawFlow(j, debugPos, producedPath.Destination);
            }
        }

        void SetColor(int flowIndex)
        {
            
            if (losmap.IsLOS(flowIndex))
            {
                Gizmos.color = Color.white;
            }
            else
            {
                Gizmos.color = Color.black;
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
        void DrawFlow(int flowIndex, Vector3 pos, float2 destination)
        {
            if (losmap.IsLOS(flowIndex))
            {
                pos = new Vector3(pos.x, yOffset, pos.z);
                float3 destination3 = new float3(destination.x, yOffset, destination.y);
                float3 dirToDes = destination3 - (float3) pos;
                dirToDes = math.normalizesafe(dirToDes) * 0.4f;
                Vector3 target = pos + (Vector3) dirToDes;
                Gizmos.DrawLine(pos, target);
                return;
            }
            pos = new Vector3(pos.x, yOffset, pos.z);
            float2 flowDir = flowField[flowIndex].GetFlow(_tileSize);
            flowDir = math.normalizesafe(flowDir) * 0.4f;
            Vector3 targetPos = pos + new Vector3(flowDir.x, 0f, flowDir.y);
            Gizmos.DrawLine(pos, targetPos);
        }
    }

    public void DebugDestination(FlowFieldAgent agent)
    {
        if (agent == null) { return; }
        Path path = agent.GetPath();
        if(path == null) { return; }

        Vector2 destination = path.Destination;
        Vector3 destination3 = new Vector3(destination.x, 0.1f, destination.y);
        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(destination3, 0.3f);
    }
}
#endif