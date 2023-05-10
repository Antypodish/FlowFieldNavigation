﻿#if (UNITY_EDITOR) 

using Unity.Collections;
using UnityEditor;
using UnityEngine;

public class AStarDebugger
{
    PathfindingManager _pathfindingManager;
    PortalNode _clickedPortalNode;
    float _tileSize;
    int _fieldRowAmount;
    int _fieldColAmount;

    public AStarDebugger(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
        _tileSize = pathfindingManager.TileSize;
        _fieldColAmount = pathfindingManager.ColumnAmount;
        _fieldRowAmount = pathfindingManager.RowAmount;
    }

    public void DebugAstarForPortal(int offset)
    {
        float yOffset = .2f;
        FieldGraph fieldGraph = _pathfindingManager.CostFieldProducer.GetCostFieldWithOffset(offset).FieldGraph;
        NativeArray<byte> costs = _pathfindingManager.CostFieldProducer.GetCostFieldWithOffset(offset).Costs;
        NativeArray<DirectionData> directions = _pathfindingManager.CostFieldProducer.GetCostFieldWithOffset(offset).Directions;
        SetClickedPortalNode(fieldGraph);
        Portal clickedPortal = _clickedPortalNode.Portal;
        AStarGrid astartGrid = new AStarGrid(_fieldRowAmount, _fieldColAmount);
        NativeArray<SectorNode> sectorNodesOfPortal = fieldGraph.GetSectorNodesOf(_clickedPortalNode);
        DebugAStarFor(sectorNodesOfPortal[0].Sector);
        DebugAStarFor(sectorNodesOfPortal[1].Sector);

        void DebugAStarFor(Sector sector)
        {
            Index2 targetIndex = sector.ContainsIndex(clickedPortal.Index1) ? clickedPortal.Index1 : clickedPortal.Index2;
            NativeArray<AStarTile> aStarTiles = astartGrid.GetIntegratedCostsFor(sector, targetIndex, costs, directions);

            Index2 lowerBound = sector.StartIndex;
            Index2 upperBound = new Index2(lowerBound.R + sector.Size, lowerBound.C + sector.Size);

            for (int r = lowerBound.R; r < upperBound.R; r++)
            {
                for(int c = lowerBound.C; c < upperBound.C; c++)
                {
                    Index2 index = new Index2(r, c);
                    float cost = aStarTiles[Index2.ToIndex(index, _fieldColAmount)].IntegratedCost;
                    Vector3 pos = new Vector3(_tileSize / 2 + index.C * _tileSize, yOffset, _tileSize / 2 + index.R * _tileSize);
                    if(cost == float.MaxValue)
                    {
                        Handles.Label(pos, "max");
                        continue;
                    }
                    Handles.Label(pos, cost.ToString());
                }
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
                    if (pickedPortalNode.Portal.Index1 == clickedIndex || pickedPortalNode.Portal.Index2 == clickedIndex)
                    {
                        _clickedPortalNode = pickedPortalNode;
                    }
                }
            }
        }
    }
}
#endif