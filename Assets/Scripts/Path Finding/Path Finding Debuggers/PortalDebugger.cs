using UnityEngine;
using Unity.Collections;
using UnityEngine.Analytics;
using UnityEditor;
using System.ComponentModel;
using Unity.VisualScripting;

public class PortalDebugger
{
    PathfindingManager _pathfindingManager;

    SectorNode _clickedSectorNodes;
    PortalNode _clickedPortalNode;
    float _tileSize;
    public PortalDebugger(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
        _tileSize = _pathfindingManager.TileSize;
    }
    
    public void DebugPortals(int offset)
    {
        Gizmos.color = Color.cyan;
        PortalArray portalArray = _pathfindingManager.CostFieldProducer.GetCostFieldWithOffset(offset).FieldGraph.PortalArray;
        NativeArray<PortalNode> portalNodes = portalArray.Nodes;
        float tileSize = _pathfindingManager.TileSize;
        float yOffset = .02f;
        for(int i = 0; i < portalNodes.Length; i++)
        {
            Gizmos.DrawCube(GetPositionOf(portalNodes[i].Portal), Vector3.one / 4);
        }
    }
    public void DebugPortalsOnClickedSector(int offset)
    {
        Gizmos.color = Color.black;
        float tileSize = _pathfindingManager.TileSize;
        FieldGraph fieldGraph = _pathfindingManager.CostFieldProducer.GetCostFieldWithOffset(offset).FieldGraph;
        SetClickedSectorNode(fieldGraph);

        NativeArray<int> portalIndicies = fieldGraph.SectorArray.GetPortalIndicies(_clickedSectorNodes, fieldGraph.WindowArray.Nodes);
        NativeArray<PortalNode> portalNodes = fieldGraph.PortalArray.Nodes;
        for(int i = 0; i < portalIndicies.Length; i++)
        {
            PortalNode pickedPortalNode = portalNodes[portalIndicies[i]];
            Gizmos.DrawSphere(GetPositionOf(pickedPortalNode.Portal), 0.35f);
        }
    }
    public void DebugCostsToClickedPortal(int offset)
    {
        float yOffset = 0.02f;
        Gizmos.color = Color.black;
        float tileSize = _pathfindingManager.TileSize;
        FieldGraph fieldGraph = _pathfindingManager.CostFieldProducer.GetCostFieldWithOffset(offset).FieldGraph;
        SetClickedPortalNode(fieldGraph);

        Gizmos.color = Color.red;
        Gizmos.DrawSphere(GetPositionOf(_clickedPortalNode.Portal), 0.35f);

        //draw costs of neighbours
        NativeArray<PortalToPortal> porPtrs = fieldGraph.PortalArray.PorPtrs;
        NativeArray<PortalNode> portalNodes = fieldGraph.PortalArray.Nodes;

        Gizmos.color = Color.black;
        for (int i = 0; i < _clickedPortalNode.PorToPorCnt; i++)
        {
            int index = porPtrs[_clickedPortalNode.PorToPorPtr + i].Index;
            float dist = porPtrs[_clickedPortalNode.PorToPorPtr + i].Distance;

            SectorNode[] sectorNodesOfPortal = fieldGraph.GetSectorNodesOf(_clickedPortalNode);
            Index2 portalIndex1 = portalNodes[index].Portal.Index1;
            Index2 portalIndex2 = portalNodes[index].Portal.Index2;
            Index2 pickedPortalIndex = portalIndex1;

            if (sectorNodesOfPortal[0].Sector.ContainsIndex(portalIndex1))
            {
                pickedPortalIndex = portalIndex1;
            }
            else if (sectorNodesOfPortal[0].Sector.ContainsIndex(portalIndex2))
            {
                pickedPortalIndex = portalIndex2;

            }
            if (sectorNodesOfPortal[1].Sector.ContainsIndex(portalIndex1))
            {
                pickedPortalIndex = portalIndex1;
            }
            else if (sectorNodesOfPortal[1].Sector.ContainsIndex(portalIndex2))
            {
                pickedPortalIndex = portalIndex2;

            }

            Vector3 pos = new Vector3(tileSize / 2 + tileSize * pickedPortalIndex.C, yOffset, tileSize / 2 + tileSize * pickedPortalIndex.R);
            Handles.Label(pos, dist.ToString());
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
        if (Input.GetMouseButton(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                Vector3 hitPos = hit.point;
                NativeArray<int> portalIndicies = fieldGraph.SectorArray.GetPortalIndicies(fieldGraph.GetSectorNodeAt(hitPos), fieldGraph.WindowArray.Nodes);
                NativeArray<PortalNode> portalNodes = fieldGraph.PortalArray.Nodes;
                Index2 clickedIndex = new Index2(Mathf.FloorToInt(hitPos.z / _tileSize), Mathf.FloorToInt(hitPos.x / _tileSize));
                for (int i = 0; i < portalIndicies.Length; i++)
                {
                    PortalNode pickedPortalNode = portalNodes[portalIndicies[i]];
                    if(pickedPortalNode.Portal.Index1 == clickedIndex || pickedPortalNode.Portal.Index2 == clickedIndex)
                    {
                        _clickedPortalNode = pickedPortalNode;
                    }
                }
            }
        }
    }
    Vector3 GetPositionOf(Portal portal)
    {
        Index2 index1 = portal.Index1;
        Index2 index2 = portal.Index2;

        Vector3 pos1 = new Vector3(_tileSize / 2 + _tileSize * index1.C, 0, _tileSize / 2 + _tileSize * index1.R);
        Vector3 pos2 = new Vector3(_tileSize / 2 + _tileSize * index2.C, 0, _tileSize / 2 + _tileSize * index2.R);

        return (pos1 + pos2) / 2;
    }
}
