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
    int _fieldTileAmount;
    float _fieldTileSize;
    int _sectorTileAmount;
    int _sectorMatrixSize;
    int _portalPerWindow;
    public FieldGraph(NativeArray<byte> costs, NativeArray<DirectionData> directions, int sectorSize, int fieldTileAmount, int costFieldOffset, float fieldTileSize)
    {
        //size calculations
        int sectorMatrixSize = fieldTileAmount / sectorSize;
        int sectorAmount = sectorMatrixSize * sectorMatrixSize;
        int windowAmount = sectorMatrixSize * ((sectorMatrixSize - 1) * 2);
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
        _fieldTileAmount = fieldTileAmount;
        _fieldTileSize = fieldTileSize;
        _sectorTileAmount = sectorSize;
        _sectorMatrixSize = sectorMatrixSize;
        _portalPerWindow = portalPerWindow;
        _costs = costs;
        _directions = directions;
        _aStarGrid = new AStarGrid(fieldTileAmount);
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
            _fieldTileAmount = _fieldTileAmount,
            _fieldTileSize = _fieldTileSize,
            _sectorTileAmount = _sectorTileAmount,
            _sectorMatrixSize = _sectorMatrixSize,
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
            WinPtrs = SectorArray.WinPtrs,
            WindowNodes = WindowArray.Nodes,
            SecPtrs = WindowArray.SecPtrs,
            PortalNodes = PortalArray.Nodes,
            PorPtrs = PortalArray.PorPtrs,
            _aStarGrid = _aStarGrid,
            _costs = _costs,
            _directions = _directions,

            _fieldTileAmount = _fieldTileAmount,
            _fieldTileSize = _fieldTileSize,
            _sectorTileAmount = _sectorTileAmount,
            _sectorMatrixSize = _sectorMatrixSize,
            _portalPerWindow = _portalPerWindow
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
        int index = Index2.ToIndex(index2, _sectorMatrixSize);
        return SectorArray.Nodes[index];
    }
    public void SetUnwalkable(Index2 bound1, Index2 bound2)
    {
        _costs[Index2.ToIndex(bound1, _fieldTileAmount)] = byte.MaxValue;
        _costs[Index2.ToIndex(bound2, _fieldTileAmount)] = byte.MaxValue;
    }
    public void ConfigureSectorNodes() => SectorArray.ConfigureSectorNodes(_fieldTileAmount, _sectorTileAmount);
    public void ConfigureWindowNodes() => WindowArray.ConfigureWindowNodes(SectorArray.Nodes, _costs, _portalPerWindow, _sectorMatrixSize, _fieldTileAmount);
    public void ConfigureSectorToWindowPoiners() => SectorArray.ConfigureSectorToWindowPoiners(WindowArray.Nodes);
    public void ConfigureWindowToSectorPointers() => WindowArray.ConfigureWindowToSectorPointers(SectorArray.Nodes);
    public void ConfigurePortalNodes() => PortalArray.ConfigurePortalNodes(WindowArray.Nodes, _costs, _fieldTileAmount, _portalPerWindow * 8 - 2);
    public void ConfigurePortalToPortalPtrs() => PortalArray.ConfigurePortalToPortalPtrs(_aStarGrid, SectorArray, WindowArray, _costs, _directions, _fieldTileAmount);
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
