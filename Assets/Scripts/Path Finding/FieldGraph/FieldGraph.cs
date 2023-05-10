using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Unity.Mathematics;

public struct FieldGraph
{
    //main graph elements
    public SectorArray SectorArray;
    public WindowArray WindowArray;
    public PortalArray PortalArray;
    AStarGrid _aStarGrid;

    //helper data
    NativeArray<byte> _costs;
    NativeArray<DirectionData> _directions;
    int _fieldRowAmount;
    int _fieldColAmount;
    float _fieldTileSize;
    int _sectorTileAmount;
    int _sectorMatrixRowAmount;
    int _sectorMatrixColAmount;
    int _portalPerWindow;
    public FieldGraph(NativeArray<byte> costs, NativeArray<DirectionData> directions, int sectorSize, int fieldRowAmount, int fieldColAmount, int costFieldOffset, float fieldTileSize)
    {
        //size calculations
        int sectorMatrixRowAmount = fieldRowAmount / sectorSize;
        int sectorMatrixColAmount = fieldColAmount / sectorSize;

        int sectorAmount = sectorMatrixRowAmount * sectorMatrixColAmount;
        int windowAmount = 2 * fieldColAmount * fieldRowAmount + fieldRowAmount - fieldColAmount;
        int winToSecPtrAmount = windowAmount * 2;
        int secToWinPtrAmount = windowAmount * 2;
        int divider = 2;
        for (int i = 0; i < costFieldOffset; i++)
        {
            divider *= 2;
        }
        int portalPerWindow = (sectorSize + divider - 1) / divider;
        int portalAmount = windowAmount * portalPerWindow;
        int porToPorPtrAmount = portalAmount * (portalPerWindow * 8 - 2);

        //innitialize fields
        _sectorMatrixColAmount = sectorMatrixColAmount;
        _sectorMatrixRowAmount = sectorMatrixRowAmount;
        _fieldColAmount = fieldColAmount;
        _fieldRowAmount = fieldRowAmount;
        _fieldTileSize = fieldTileSize;
        _sectorTileAmount = sectorSize;
        _portalPerWindow = portalPerWindow;
        _costs = costs;
        _directions = directions;
        _aStarGrid = new AStarGrid(fieldRowAmount, fieldColAmount);
        SectorArray = new SectorArray(sectorAmount, secToWinPtrAmount);
        WindowArray = new WindowArray(windowAmount, winToSecPtrAmount);
        PortalArray = new PortalArray(portalAmount, porToPorPtrAmount);
    }
    public FieldGraphConfigurationJob GetConfigJob()
    {
        return new FieldGraphConfigurationJob()
        {
            SectorNodes = SectorArray.Nodes,
            WinPtrs = SectorArray.WinPtrs,
            WindowNodes = WindowArray.Nodes,
            SecPtrs = WindowArray.SecPtrs,
            PortalNodes = PortalArray.Nodes,
            PorPtrs = PortalArray.PorPtrs,
            _costs = _costs,
            _directions = _directions,
            _fieldColAmount = _fieldColAmount,
            _fieldRowAmount = _fieldRowAmount,
            _fieldTileSize = _fieldTileSize,
            _sectorTileAmount = _sectorTileAmount,
            _sectorColAmount = _sectorMatrixColAmount,
            _sectorRowAmount = _sectorMatrixRowAmount,
            _portalPerWindow = _portalPerWindow,
            _integratedCosts = _aStarGrid._integratedCosts,
            _searchQueue = _aStarGrid._searchQueue,
        };
    }
    public CostFieldEditJob GetEditJob(Index2 bound1, Index2 bound2)
    {
        return new CostFieldEditJob()
        {
            Bound1 = bound1,
            Bound2 = bound2,
            SectorNodes = SectorArray.Nodes,
            SecToWinPtrs = SectorArray.WinPtrs,
            WindowNodes = WindowArray.Nodes,
            WinToSecPtrs = WindowArray.SecPtrs,
            PortalNodes = PortalArray.Nodes,
            PorPtrs = PortalArray.PorPtrs,
            _costs = _costs,
            _directions = _directions,

            _fieldTileAmount = _fieldColAmount,//*
            _fieldTileSize = _fieldTileSize,
            _sectorTileAmount = _sectorTileAmount,
            _sectorMatrixSize = _fieldColAmount,//*
            _portalPerWindow = _portalPerWindow,
            _integratedCosts = _aStarGrid._integratedCosts,
            _searchQueue = _aStarGrid._searchQueue,
            debugArray = new NativeArray<WindowPair>(1000, Allocator.Persistent),
            windowCount = new NativeArray<int>(1, Allocator.Persistent)
        };
    }
    public NativeArray<WindowNode> GetWindowNodesOf(SectorNode sectorNode)
    {
        NativeArray <WindowNode> windowNodes = new NativeArray<WindowNode>(sectorNode.SecToWinCnt, Allocator.Temp);
        for(int i = sectorNode.SecToWinPtr; i < sectorNode.SecToWinPtr + sectorNode.SecToWinCnt; i++)
        {
            windowNodes[i - sectorNode.SecToWinPtr] = WindowArray.Nodes[SectorArray.WinPtrs[i]];
        }
        return windowNodes;
    }
    public NativeArray<SectorNode> GetSectorNodesOf(WindowNode windowNode)
    {
        NativeArray<SectorNode> sectorNodes = new NativeArray<SectorNode>(windowNode.WinToSecCnt, Allocator.Temp);
        for (int i = windowNode.WinToSecPtr; i < windowNode.WinToSecPtr + windowNode.WinToSecCnt; i++)
        {
            sectorNodes[i - windowNode.WinToSecPtr] = SectorArray.Nodes[WindowArray.SecPtrs[i]];
        }
        return sectorNodes;
    }
    public NativeArray<SectorNode> GetSectorNodesOf(PortalNode portal)
    {
        WindowNode windowNode = WindowArray.Nodes[portal.WinPtr];
        NativeArray<SectorNode> sectorNodes = new NativeArray<SectorNode>(2, Allocator.Temp);

        for (int i = windowNode.WinToSecPtr; i < windowNode.WinToSecPtr + windowNode.WinToSecCnt; i++)
        {
            sectorNodes[i - windowNode.WinToSecPtr] = SectorArray.Nodes[WindowArray.SecPtrs[i]];
        }
        return sectorNodes;

    }
    public NativeArray<int> GetWindowNodeIndiciesOf(SectorNode sectorNode)
    {
        NativeArray<int> windowNodeIndicies = new NativeArray<int>(sectorNode.SecToWinCnt, Allocator.Temp);
        for (int i = sectorNode.SecToWinPtr; i < sectorNode.SecToWinPtr + sectorNode.SecToWinCnt; i++)
        {
            windowNodeIndicies[i - sectorNode.SecToWinPtr] = SectorArray.WinPtrs[i];
        }
        return windowNodeIndicies;
    }
    public NativeArray<int> GetSectorNodeIndiciesOf(WindowNode windowNode)
    {
        NativeArray<int> sectorNodeIndicies = new NativeArray<int>(windowNode.WinToSecCnt, Allocator.Temp);
        for (int i = windowNode.WinToSecPtr; i < windowNode.WinToSecPtr + windowNode.WinToSecCnt; i++)
        {
            sectorNodeIndicies[i - windowNode.WinToSecPtr] = WindowArray.SecPtrs[i];
        }
        return sectorNodeIndicies;
    }
    public NativeArray<int> GetSectorNodeIndiciesOf(PortalNode portal)
    {
        WindowNode windowNodeIndex = WindowArray.Nodes[portal.WinPtr];
        NativeArray<int> sectorNodeIndicies = new NativeArray<int>(2, Allocator.Temp);

        for (int i = windowNodeIndex.WinToSecPtr; i < windowNodeIndex.WinToSecPtr + windowNodeIndex.WinToSecCnt; i++)
        {
            sectorNodeIndicies[i - windowNodeIndex.WinToSecPtr] = WindowArray.SecPtrs[i];
        }
        return sectorNodeIndicies;
    }
    public SectorNode GetSectorNodeAt(Vector3 pos)
    {
        float sectorSize = _sectorTileAmount * _fieldTileSize;
        Index2 index2 = new Index2(Mathf.FloorToInt(pos.z / sectorSize), Mathf.FloorToInt(pos.x / sectorSize));
        int index = Index2.ToIndex(index2, _sectorMatrixColAmount);
        return SectorArray.Nodes[index];
    }
    public void SetUnwalkable(Index2 bound1, Index2 bound2)
    {
        _costs[Index2.ToIndex(bound1, _fieldColAmount)] = byte.MaxValue;
        _costs[Index2.ToIndex(bound2, _fieldColAmount)] = byte.MaxValue;
    }
}
public struct AStarTile
{
    public bool Enqueued;
    public float IntegratedCost;

    public AStarTile(float integratedCost, bool enqueued)
    {
        Enqueued = enqueued;
        IntegratedCost = integratedCost;
    }
}
