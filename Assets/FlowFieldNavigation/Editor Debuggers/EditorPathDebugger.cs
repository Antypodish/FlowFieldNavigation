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
using Unity.Jobs;
using static UnityEditor.PlayerSettings;

internal class EditorPathDebugger
{
    PathDataContainer _pathContainer;
    PathfindingManager _pathfindingManager;
    FieldDataContainer _fieldProducer;
    float _tileSize;

    internal EditorPathDebugger(PathfindingManager pathfindingManager)
    {
        _pathContainer = pathfindingManager.PathDataContainer;
        _pathfindingManager = pathfindingManager;
        _fieldProducer = pathfindingManager.FieldDataContainer;
        _tileSize = FlowFieldUtilities.TileSize;
    }
    internal void DebugDynamicAreaIntegration(FlowFieldAgent agent)
    {
        if (_pathContainer == null) { return; }
        if (_pathContainer.PathfindingInternalDataList.Count == 0) { return; }
        int pathIndex = agent.GetPathIndex();
        if(pathIndex == -1) { return; }

        PathfindingInternalData internalData = _pathContainer.PathfindingInternalDataList[pathIndex];
        PathLocationData locationData = _pathfindingManager.PathDataContainer.PathLocationDataList[pathIndex];
        UnsafeList<SectorFlowStart> pickedSectorFlowStarts = locationData.DynamicAreaPickedSectorFlowStarts;
        NativeArray<IntegrationTile> integrationField = internalData.DynamicArea.IntegrationField;
        int sectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount;
        int sectorColAmount = FlowFieldUtilities.SectorColAmount;
        float tileSize = FlowFieldUtilities.TileSize;
        float sectorSize = tileSize * sectorColAmount;
        float2 fieldGridStartPos = FlowFieldUtilities.FieldGridStartPosition;
        for (int i = 0; i < pickedSectorFlowStarts.Length; i++)
        {
            int pickedSectorFlowStart = pickedSectorFlowStarts[i].FlowStartIndex;
            int pickedSectorIndex = pickedSectorFlowStarts[i].SectorIndex;

            for (int j = pickedSectorFlowStart; j < pickedSectorFlowStart + FlowFieldUtilities.SectorTileAmount; j++)
            {
                int local1d = (j - 1) % FlowFieldUtilities.SectorTileAmount;
                float2 debugPos = FlowFieldUtilities.LocalIndexToPos(local1d, pickedSectorIndex, sectorMatrixColAmount, sectorColAmount, tileSize, sectorSize, fieldGridStartPos);
                Vector3 debugPos3 = new Vector3(debugPos.x, 0.02f, debugPos.y);
                float cost = integrationField[j].Cost;
                if (cost != float.MaxValue)
                {
                    Handles.Label(debugPos3, cost.ToString());
                }
            }
        }
    }
    internal void DebugDynamicAreaFlow(FlowFieldAgent agent)
    {
        if (_pathContainer == null) { return; }
        if (_pathContainer.PathfindingInternalDataList.Count == 0) { return; }
        int pathIndex = agent.GetPathIndex();
        if (pathIndex == -1) { return; }

        float yOffset = 0.2f;
        PathDestinationData destinationData = _pathfindingManager.PathDataContainer.PathDestinationDataList[pathIndex];
        PathLocationData locationData = _pathfindingManager.PathDataContainer.PathLocationDataList[pathIndex];
        PathFlowData flowData = _pathfindingManager.PathDataContainer.PathFlowDataList[pathIndex];
        UnsafeList<SectorFlowStart> pickedSectorFlowStarts = locationData.DynamicAreaPickedSectorFlowStarts;
        UnsafeList<FlowData> flowField = flowData.DynamicAreaFlowField;
        Gizmos.color = Color.blue;
        int sectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount;
        int sectorColAmount = FlowFieldUtilities.SectorColAmount;
        float tileSize = FlowFieldUtilities.TileSize;
        float sectorSize = tileSize * sectorColAmount;
        float2 fieldGridStartPos = FlowFieldUtilities.FieldGridStartPosition;
        for (int i = 0; i < pickedSectorFlowStarts.Length; i++)
        {
            int pickedSectorFlowStart = pickedSectorFlowStarts[i].FlowStartIndex;
            int pickedSectorIndex = pickedSectorFlowStarts[i].SectorIndex;

            for (int j = pickedSectorFlowStart; j < pickedSectorFlowStart + FlowFieldUtilities.SectorTileAmount; j++)
            {
                int local1d = (j - 1) % FlowFieldUtilities.SectorTileAmount;
                float2 debugPos = FlowFieldUtilities.LocalIndexToPos(local1d, pickedSectorIndex, sectorMatrixColAmount, sectorColAmount, tileSize, sectorSize, fieldGridStartPos);
                Vector3 debugPos3 = new Vector3(debugPos.x, 0.02f, debugPos.y);
                if(j >= flowField.Length) { continue; }
                FlowData flow = flowField[j];
                if (!flow.IsValid()) { continue; }
                DrawSquare(debugPos3, 0.2f);
                DrawFlow(j, debugPos3, destinationData.Destination);
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
            pos = new Vector3(pos.x, yOffset, pos.z);
            float2 flowDir = flowField[flowIndex].GetFlow(_tileSize);
            flowDir = math.normalizesafe(flowDir) * 0.4f;
            Vector3 targetPos = pos + new Vector3(flowDir.x, 0f, flowDir.y);
            Gizmos.DrawLine(pos, targetPos);
        }
    }
    internal void DebugActiveWaveFronts(FlowFieldAgent agent)
    {
        if (_pathContainer == null) { return; }
        if (_pathContainer.PathfindingInternalDataList.Count == 0) { return; }
        int pathIndex = agent.GetPathIndex();
        if (pathIndex == -1) { return; }
        PathfindingInternalData internalData = _pathContainer.PathfindingInternalDataList[pathIndex];

        float tileSize = FlowFieldUtilities.TileSize;
        int sectorColAmount = FlowFieldUtilities.SectorColAmount;
        NativeArray<UnsafeList<ActiveWaveFront>> waveFronts = internalData.ActivePortalList;
        NativeArray<int> pickedToSector = internalData.PickedSectorList;
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
                float2 pos = FlowFieldUtilities.IndexToPos(general2d, tileSize, FlowFieldUtilities.FieldGridStartPosition);
                Vector3 pos3 = new Vector3(pos.x, 0f, pos.y);
                Gizmos.DrawCube(pos3, new Vector3(0.6f, 0.6f, 0.6f));
            }            
        }
    }
    internal void DebugPortalTraversalMarks(FlowFieldAgent agent)
    {
        if (_pathContainer == null) { return; }
        if(_pathContainer.PathfindingInternalDataList.Count == 0) { return; }
        int pathIndex = agent.GetPathIndex();
        if (pathIndex == -1) { return; }


        PathPortalTraversalData portalTraversalData = _pathContainer.PathPortalTraversalDataList[pathIndex];
        PathDestinationData destinationData = _pathfindingManager.PathDataContainer.PathDestinationDataList[pathIndex];
        float tileSize = FlowFieldUtilities.TileSize;
        FieldGraph fg = _fieldProducer.GetFieldGraphWithOffset(destinationData.Offset);
        NativeArray<PortalNode> portalNodes = fg.PortalNodes;
        NativeArray<PortalTraversalData> portalTraversalDataArray = portalTraversalData.PortalTraversalDataArray;

        for (int i = 0; i < portalNodes.Length; i++)
        {
            PortalTraversalData travData = portalTraversalDataArray[i];
            PortalNode node = portalNodes[i];
            if (travData.HasMark(PortalTraversalMark.DijkstraPicked))
            {
                Gizmos.color = Color.white;
                Vector3 portalPos = node.GetPosition(tileSize, FlowFieldUtilities.FieldGridStartPosition);
                Handles.Label(portalPos + new Vector3(0, 0, 0.75f), "d: " + travData.DistanceFromTarget.ToString());
                Gizmos.DrawSphere(portalPos, 0.25f);
            }
            else if (travData.HasMark(PortalTraversalMark.Reduced))
            {
                Gizmos.color = Color.black;
                Vector3 portalPos = node.GetPosition(tileSize, FlowFieldUtilities.FieldGridStartPosition);
                Handles.Label(portalPos + new Vector3(0, 0, 0.75f), "d: " + travData.DistanceFromTarget.ToString());
                Gizmos.DrawSphere(portalPos, 0.25f);
            }
        }
    }
    internal void DebugTargetNeighbourPortals(FlowFieldAgent agent)
    {
        if (_pathContainer == null) { return; }
        if(_pathContainer.PathfindingInternalDataList.Count == 0) { return; }
        int pathIndex = agent.GetPathIndex();
        if (pathIndex == -1) { return; }

        PathPortalTraversalData portalTraversalData = _pathContainer.PathPortalTraversalDataList[pathIndex];
        PathDestinationData destinationData = _pathfindingManager.PathDataContainer.PathDestinationDataList[pathIndex];
        float tileSize = FlowFieldUtilities.TileSize;
        FieldGraph fg = _fieldProducer.GetFieldGraphWithOffset(destinationData.Offset);
        NativeArray<PortalNode> portalNodes = fg.PortalNodes;
        NativeArray<PortalTraversalData> portalTraversalDataArray = portalTraversalData.PortalTraversalDataArray;

        for (int i = 0; i < portalNodes.Length; i++)
        {
            PortalTraversalData travData = portalTraversalDataArray[i];
            PortalNode node = portalNodes[i];
            if (travData.HasMark(PortalTraversalMark.TargetNeighbour))
            {
                Gizmos.color = Color.magenta;
                Vector3 portalPos = node.GetPosition(tileSize, FlowFieldUtilities.FieldGridStartPosition);
                Handles.Label(portalPos + new Vector3(0, 0, 0.75f), "d: " + travData.DistanceFromTarget.ToString());
                Gizmos.DrawSphere(portalPos, 0.25f);
            }
        }
    }
    internal void DebugPortalSequence(FlowFieldAgent agent)
    {
        if (_pathContainer == null) { return; }
        if (_pathContainer.PathfindingInternalDataList.Count == 0) { return; }
        int pathIndex = agent.GetPathIndex();
        if (pathIndex == -1) { return; }

        PathPortalTraversalData portalTraversalData = _pathContainer.PathPortalTraversalDataList[pathIndex];
        PathDestinationData destinationData = _pathfindingManager.PathDataContainer.PathDestinationDataList[pathIndex];
        FieldGraph fg = _fieldProducer.GetFieldGraphWithOffset(destinationData.Offset);
        NativeArray<PortalNode> portalNodes = fg.PortalNodes;
        NativeList<ActivePortal> porSeq = portalTraversalData.PortalSequence;
        NativeList<int> portSeqBorders = portalTraversalData.PortalSequenceBorders;
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
                Vector3 secondPorPos = secondportalNode.GetPosition(_tileSize, FlowFieldUtilities.FieldGridStartPosition);
                Vector3 firstPorPos = firstportalNode.GetPosition(_tileSize, FlowFieldUtilities.FieldGridStartPosition);

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
    internal void DebugPickedSectors(FlowFieldAgent agent)
    {
        if (_pathContainer == null) { return; }
        if (_pathContainer.PathfindingInternalDataList.Count == 0) { return; }
        int pathIndex = agent.GetPathIndex();
        if (pathIndex == -1) { return; }

        PathLocationData locationData = _pathfindingManager.PathDataContainer.PathLocationDataList[pathIndex];
        PathDestinationData destinationData = _pathfindingManager.PathDataContainer.PathDestinationDataList[pathIndex];
        SectorBitArray sectorBitArray = _pathfindingManager.PathDataContainer.PathSectorBitArrays[pathIndex];
        float yOffset = 0.3f;
        Gizmos.color = Color.black;
        FieldGraph fg = _fieldProducer.GetFieldGraphWithOffset(destinationData.Offset);
        NativeArray<SectorNode> sectorNodes = fg.SectorNodes;
        UnsafeList<int> sectorMarks = locationData.SectorToPicked;

        for(int i = 0; i < sectorMarks.Length; i++)
        {
            if (sectorMarks[i] == 0 && sectorBitArray.HasBit(i)) { UnityEngine.Debug.Log("woooo"); }
            if (sectorMarks[i] != 0 && !sectorBitArray.HasBit(i)) { UnityEngine.Debug.Log("woooo"); }
        }

        for (int i = 0; i < sectorMarks.Length; i++)
        {
            if (sectorMarks[i] == 0) { continue; }
            int2 sector2d = FlowFieldUtilities.To2D(i, FlowFieldUtilities.SectorMatrixColAmount);
            float sectorSize = FlowFieldUtilities.SectorColAmount * FlowFieldUtilities.TileSize;
            float2 sectorPos = FlowFieldUtilities.IndexToPos(sector2d, sectorSize, FlowFieldUtilities.FieldGridStartPosition);
            float2 botLeft2 = sectorPos + new float2(-sectorSize / 2, -sectorSize / 2);
            float2 topLeft2 = sectorPos + new float2(-sectorSize / 2, sectorSize / 2);
            float2 botRight2 = sectorPos + new float2(sectorSize / 2, -sectorSize / 2);
            float2 topRight2 = sectorPos + new float2(sectorSize / 2, sectorSize / 2);
            float3 botLeft3 = new float3(botLeft2.x, yOffset, botLeft2.y);
            float3 topLeft3 = new float3(topLeft2.x, yOffset, topLeft2.y);
            float3 botRight3 = new float3(botRight2.x, yOffset, botRight2.y);
            float3 topRight3 = new float3(topRight2.x, yOffset, topRight2.y);
            Gizmos.DrawLine(botLeft3, topLeft3);
            Gizmos.DrawLine(topLeft3, topRight3);
            Gizmos.DrawLine(topRight3, botRight3);
            Gizmos.DrawLine(botRight3, botLeft3);
        }
    }
    internal void DebugIntegrationField(FlowFieldAgent agent)
    {
        if (_pathContainer == null) { return; }
        if (_pathContainer.PathfindingInternalDataList.Count == 0) { return; }
        int pathIndex = agent.GetPathIndex();
        if (pathIndex == -1) { return; }

        PathfindingInternalData internalData = _pathContainer.PathfindingInternalDataList[pathIndex];

        PathLocationData locationData = _pathfindingManager.PathDataContainer.PathLocationDataList[pathIndex];
        PathDestinationData destinationData = _pathfindingManager.PathDataContainer.PathDestinationDataList[pathIndex];
        int sectorColAmount = FlowFieldUtilities.SectorColAmount;
        int sectorTileAmount = sectorColAmount * sectorColAmount;
        UnsafeList<int> sectorMarks = locationData.SectorToPicked;
        NativeArray<IntegrationTile> integrationField = internalData.IntegrationField;
        for (int i = 0; i < sectorMarks.Length; i++)
        {
            if (sectorMarks[i] == 0) { continue; }
            int pickedStartIndex = sectorMarks[i];
            for (int j = pickedStartIndex; j < pickedStartIndex + sectorTileAmount; j++)
            {
                int local1d = (j - 1) % sectorTileAmount;
                int2 general2d = FlowFieldUtilities.GetGeneral2d(local1d, i, FlowFieldUtilities.SectorMatrixColAmount, sectorColAmount);
                float2 debugPos2 = FlowFieldUtilities.IndexToPos(general2d, _tileSize, FlowFieldUtilities.FieldGridStartPosition);
                float3 debugPos = new float3(debugPos2.x, 0.02f, debugPos2.y);
                float cost = integrationField[j].Cost;
                if (cost != float.MaxValue)
                {
                    Handles.Label(debugPos, cost.ToString());
                }
            }
            
        }
    }
    internal void LOSBlockDebug(FlowFieldAgent agent)
    {
        if (_pathContainer == null) { return; }
        if (_pathContainer.PathfindingInternalDataList.Count == 0) { return; }
        int pathIndex = agent.GetPathIndex();
        if (pathIndex == -1) { return; }

        PathfindingInternalData internalData = _pathContainer.PathfindingInternalDataList[pathIndex];

        PathLocationData locationData = _pathfindingManager.PathDataContainer.PathLocationDataList[pathIndex];
        PathDestinationData destinationData = _pathfindingManager.PathDataContainer.PathDestinationDataList[pathIndex];
        UnsafeList<int> sectorMarks = locationData.SectorToPicked;
        NativeArray<IntegrationTile> integrationField = internalData.IntegrationField;
        int sectorColAmount = FlowFieldUtilities.SectorColAmount;
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
                    int local1d = (j - 1) % sectorTileAmount;
                    int2 general2d = FlowFieldUtilities.GetGeneral2d(local1d, i, FlowFieldUtilities.SectorMatrixColAmount, sectorColAmount);
                    float2 debugPos2 = FlowFieldUtilities.IndexToPos(general2d, _tileSize, FlowFieldUtilities.FieldGridStartPosition);
                    float3 debugPos = new float3(debugPos2.x, 0.02f, debugPos2.y);
                    Gizmos.DrawCube(debugPos, new Vector3(0.3f, 0.3f, 0.3f));
                }
                else if ((integrationField[j].Mark & IntegrationMark.LOSC) == IntegrationMark.LOSC)
                {
                    Gizmos.color = Color.black;
                    int local1d = (j - 1) % sectorTileAmount;
                    int2 general2d = FlowFieldUtilities.GetGeneral2d(local1d, i, FlowFieldUtilities.SectorMatrixColAmount, sectorColAmount);
                    float2 debugPos2 = FlowFieldUtilities.IndexToPos(general2d, _tileSize, FlowFieldUtilities.FieldGridStartPosition);
                    float3 debugPos = new float3(debugPos2.x, 0.02f, debugPos2.y);
                    Gizmos.DrawCube(debugPos, new Vector3(0.3f, 0.3f, 0.3f));
                }
            } 
        }
    }
    internal void DebugFlowField(FlowFieldAgent agent)
    {
        if (_pathContainer == null) { return; }
        if (_pathContainer.PathfindingInternalDataList.Count == 0) { return; }
        int pathIndex = agent.GetPathIndex();
        if (pathIndex == -1) { return; }

        PathfindingInternalData internalData = _pathContainer.PathfindingInternalDataList[pathIndex];

        PathDestinationData destinationData = _pathfindingManager.PathDataContainer.PathDestinationDataList[pathIndex];
        PathLocationData locationData = _pathfindingManager.PathDataContainer.PathLocationDataList[pathIndex];
        PathFlowData pathFlowData = _pathfindingManager.PathDataContainer.PathFlowDataList[pathIndex];
        float yOffset = 0.2f;
        UnsafeList<int> sectorMarks = locationData.SectorToPicked;
        UnsafeList<FlowData> flowField = pathFlowData.FlowField;
        UnsafeLOSBitmap losmap = pathFlowData.LOSMap;
        UnsafeList<SectorFlowStart> dynamicAreaFlowStarts = locationData.DynamicAreaPickedSectorFlowStarts;
        UnsafeList<FlowData> dynamicAreaFlowField = internalData.DynamicArea.FlowFieldCalculationBuffer;
        int sectorColAmount = FlowFieldUtilities.SectorColAmount;
        int sectorTileAmount = sectorColAmount * sectorColAmount;
        for (int i = 0; i < sectorMarks.Length; i++)
        {
            if (sectorMarks[i] == 0) { continue; }
            int pickedStartIndex = sectorMarks[i];
            for (int j = pickedStartIndex; j < pickedStartIndex + sectorTileAmount; j++)
            {
                int local1d = (j - 1) % sectorTileAmount;
                int2 general2d = FlowFieldUtilities.GetGeneral2d(local1d, i, FlowFieldUtilities.SectorMatrixColAmount, sectorColAmount);
                float2 debugPos2 = FlowFieldUtilities.IndexToPos(general2d, _tileSize, FlowFieldUtilities.FieldGridStartPosition);
                float3 debugPos = new float3(debugPos2.x, 0.02f, debugPos2.y);
                if (j >= flowField.Length) { continue; }
                if(HasLOS(i, local1d))
                {
                    Gizmos.color = Color.white;
                    DrawSquare(debugPos, 0.2f);
                    DrawLOS(debugPos, destinationData.Destination);
                }
                else if(HasDynamicFlow(i, local1d))
                {
                    Gizmos.color = Color.blue;
                    DrawSquare(debugPos, 0.2f);
                    DrawFlow(GetDynamicFlow(i, local1d), debugPos);
                }
                else
                {
                    Gizmos.color = Color.black;
                    FlowData flowData = GetFlow(i, local1d);
                    if (flowData.IsValid())
                    {
                        DrawSquare(debugPos, 0.2f);
                        DrawFlow(flowData, debugPos);
                    }
                }
            }
        }
        bool HasLOS(int sectorIndex, int localIndex)
        {
            return losmap.IsLOS(sectorMarks[sectorIndex] + localIndex);
        }
        bool HasDynamicFlow(int sectorIndex, int localIndex)
        {
            int sectorFlowStart = 0;
            for(int i = 0; i < dynamicAreaFlowStarts.Length; i++)
            {
                SectorFlowStart flowStart = dynamicAreaFlowStarts[i];
                sectorFlowStart = math.select(sectorFlowStart, flowStart.FlowStartIndex, flowStart.SectorIndex == sectorIndex);
            }
            if(sectorFlowStart == 0) { return false; }

            return dynamicAreaFlowField[sectorFlowStart + localIndex].IsValid();
        }

        FlowData GetFlow(int sectorIndex, int localIndex)
        {
            return flowField[sectorMarks[sectorIndex] + localIndex];
        }

        FlowData GetDynamicFlow(int sectorIndex, int localIndex)
        {
            int sectorFlowStart = 0;
            for (int i = 0; i < dynamicAreaFlowStarts.Length; i++)
            {
                SectorFlowStart flowStart = dynamicAreaFlowStarts[i];
                sectorFlowStart = math.select(sectorFlowStart, flowStart.FlowStartIndex, flowStart.SectorIndex == sectorIndex);
            }
            return dynamicAreaFlowField[sectorFlowStart + localIndex];
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
        void DrawLOS(Vector3 pos, float2 destination)
        {
            pos = new Vector3(pos.x, yOffset, pos.z);
            float3 destination3 = new float3(destination.x, yOffset, destination.y);
            float3 dirToDes = destination3 - (float3)pos;
            dirToDes = math.normalizesafe(dirToDes) * 0.4f;
            Vector3 target = pos + (Vector3)dirToDes;
            Gizmos.DrawLine(pos, target);
        }
        void DrawFlow(FlowData flowData, Vector3 pos)
        {
            pos = new Vector3(pos.x, yOffset, pos.z);
            float2 flowDir = flowData.GetFlow(_tileSize);
            flowDir = math.normalizesafe(flowDir) * 0.4f;
            Vector3 targetPos = pos + new Vector3(flowDir.x, 0f, flowDir.y);
            Gizmos.DrawLine(pos, targetPos);
        }
    }

    internal void DebugDestination(FlowFieldAgent agent)
    {
        if (agent == null) { return; }
        int pathIndex = agent.GetPathIndex();
        if (pathIndex == -1) { return; }

        PathDestinationData destinationData = _pathfindingManager.PathDataContainer.PathDestinationDataList[_pathfindingManager.Interface.GetPathIndex(agent.AgentDataIndex)];
        Vector2 destination = destinationData.Destination;
        Vector3 destination3 = new Vector3(destination.x, 0.1f, destination.y);
        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(destination3, 0.3f);
    }
}
#endif