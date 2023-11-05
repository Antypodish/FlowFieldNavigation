#if (UNITY_EDITOR)

using UnityEngine;
using Unity.Collections;
using UnityEditor;
using Unity.Collections.LowLevel.Unsafe;


public class EditorPortalDebugger
{
    PathfindingManager _pathfindingManager;

    SectorNode _clickedSectorNodes;
    PortalNode _clickedPortalNode;
    public EditorPortalDebugger(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
    }
    
    public void DebugPortals(int offset)
    {
        Gizmos.color = Color.cyan;
        FieldGraph fieldGraph = _pathfindingManager.FieldProducer.GetFieldGraphWithOffset(offset);
        NativeArray<WindowNode> windowNodes = fieldGraph.WindowNodes;
        UnsafeList<PortalNode> portalNodes = fieldGraph.PortalNodes;
        for(int i = 0; i < windowNodes.Length; i++)
        {
            int porPtr = windowNodes[i].PorPtr;
            int porCnt = windowNodes[i].PorCnt;
            for(int j = 0; j < porCnt; j++)
            {
                PortalNode pickedPortalNode = portalNodes[porPtr + j];
                if (pickedPortalNode.Portal1.Index == pickedPortalNode.Portal2.Index) { continue; }
                Gizmos.DrawCube(GetPositionOf(pickedPortalNode), Vector3.one / 4);
            }
        }
    }
    public void DebugPortalsOnClickedSector(int offset)
    {
        Gizmos.color = Color.white;
        float tileSize = _pathfindingManager.TileSize;
        FieldGraph fieldGraph = _pathfindingManager.FieldProducer.GetFieldGraphWithOffset(offset);
        SetClickedSectorNode(fieldGraph);

        NativeArray<int> portalIndicies = fieldGraph.GetPortalIndicies(_clickedSectorNodes, fieldGraph.WindowNodes);
        UnsafeList<PortalNode> portalNodes = fieldGraph.PortalNodes;
        for(int i = 0; i < portalIndicies.Length; i++)
        {
            PortalNode pickedPortalNode = portalNodes[portalIndicies[i]];
            Gizmos.DrawSphere(GetPositionOf(pickedPortalNode)+new Vector3(0,0.5f,0f), 0.5f);
        }
    }
    public void DebugCostsToClickedPortal(int offset)
    {
        float yOffset = 0.02f;
        Gizmos.color = Color.black;
        float tileSize = _pathfindingManager.TileSize;
        FieldGraph fieldGraph = _pathfindingManager.FieldProducer.GetFieldGraphWithOffset(offset);
        SetClickedPortalNode(fieldGraph);

        //debug clicked portal
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(GetPositionOf(_clickedPortalNode), 0.35f);

        //debug neighbours of clicked portal
        NativeArray<PortalToPortal> porPtrs = fieldGraph.PorToPorPtrs;
        UnsafeList<PortalNode> portalNodes = fieldGraph.PortalNodes;
        Gizmos.color = Color.black;
        DebugNeighboursAt(_clickedPortalNode.Portal1);
        DebugNeighboursAt(_clickedPortalNode.Portal2);

        //HELPERS
        void DebugNeighboursAt(Portal portal)
        {
            int porToPorPtr = portal.PorToPorPtr;
            for (int i = 0; i < portal.PorToPorCnt; i++)
            {
                int index = porPtrs[porToPorPtr + i].Index;
                float dist = porPtrs[porToPorPtr + i].Distance;

                NativeArray<SectorNode> sectorNodesOfPortal = fieldGraph.GetSectorNodesOf(_clickedPortalNode);
                Portal portal1 = portalNodes[index].Portal1;
                Portal portal2 = portalNodes[index].Portal2;
                Index2 portalIndex1 = portal1.Index;
                Index2 portalIndex2 = portal2.Index;

                if (sectorNodesOfPortal[0].Sector.ContainsIndex(portalIndex1))
                {
                    DrawDistanceAtIndex(portalIndex1, dist);
                    DrawDirectionOfPortal(portalNodes[index], portalIndex1);
                }
                else if (sectorNodesOfPortal[0].Sector.ContainsIndex(portalIndex2))
                {
                    DrawDistanceAtIndex(portalIndex2, dist);
                    DrawDirectionOfPortal(portalNodes[index], portalIndex2);

                }
                if (sectorNodesOfPortal[1].Sector.ContainsIndex(portalIndex1))
                {
                    DrawDistanceAtIndex(portalIndex1, dist);
                    DrawDirectionOfPortal(portalNodes[index], portalIndex1);
                }
                else if (sectorNodesOfPortal[1].Sector.ContainsIndex(portalIndex2))
                {
                    DrawDistanceAtIndex(portalIndex2, dist);
                    DrawDirectionOfPortal(portalNodes[index], portalIndex2);
                }
            }
            void DrawDistanceAtIndex(Index2 index, float distance)
            {
                Vector3 pos = new Vector3(tileSize / 2 + tileSize * index.C, yOffset, tileSize / 2 + tileSize * index.R);
                Handles.Label(pos, distance.ToString());
            }
            void DrawDirectionOfPortal(PortalNode portalNode, Index2 index)
            {
                Vector3 start = GetPositionOf(portalNode);
                Vector3 target = new Vector3(tileSize / 2 + tileSize * index.C, yOffset, tileSize / 2 + tileSize * index.R);

                Gizmos.DrawLine(start, target);
            }
        }        
    }
    void SetClickedSectorNode(FieldGraph fieldGraph)
    {
        if (Input.GetMouseButton(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                Vector3 hitPos = hit.point;
                _clickedSectorNodes = fieldGraph.GetSectorNodeAt(hitPos);
            }
        }
    }
    void SetClickedPortalNode(FieldGraph fieldGraph)
    {
        float tileSize = _pathfindingManager.TileSize;
        if (Input.GetMouseButton(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                Vector3 hitPos = hit.point;
                NativeArray<int> portalIndicies = fieldGraph.GetPortalIndicies(fieldGraph.GetSectorNodeAt(hitPos), fieldGraph.WindowNodes);
                UnsafeList<PortalNode> portalNodes = fieldGraph.PortalNodes;
                Index2 clickedIndex = new Index2(Mathf.FloorToInt(hitPos.z / tileSize), Mathf.FloorToInt(hitPos.x / tileSize));
                for (int i = 0; i < portalIndicies.Length; i++)
                {
                    PortalNode pickedPortalNode = portalNodes[portalIndicies[i]];
                    if(pickedPortalNode.Portal1.Index == clickedIndex || pickedPortalNode.Portal2.Index == clickedIndex)
                    {
                        _clickedPortalNode = pickedPortalNode;
                    }
                }
            }
        }
    }
    Vector3 GetPositionOf(PortalNode portalNode)
    {
        float tileSize = _pathfindingManager.TileSize;

        Index2 index1 = portalNode.Portal1.Index;
        Index2 index2 = portalNode.Portal2.Index;

        Vector3 pos1 = new Vector3(tileSize / 2 + tileSize * index1.C, 0, tileSize / 2 + tileSize * index1.R);
        Vector3 pos2 = new Vector3(tileSize / 2 + tileSize * index2.C, 0, tileSize / 2 + tileSize * index2.R);

        return (pos1 + pos2) / 2;
    }
}
#endif
