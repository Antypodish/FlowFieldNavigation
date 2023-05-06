using UnityEngine;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;

public struct FieldGraph
{
    public SectorArray SectorArray;
    public WindowArray WindowArray;
    public PortalArray PortalArray;
    
    NativeArray<byte> _costs;
    NativeArray<DirectionData> _directions;
    public AStarGrid _aStarGrid;

    int _fieldTileAmount;
    float _fieldTileSize;
    int _sectorTileAmount;
    int _sectorMatrixSize;
    int _portalPerWindow;
    public FieldGraph(int sectorSize, int fieldTileAmount, int costFieldOffset, float fieldTileSize, NativeArray<byte> costs, NativeArray<DirectionData> directions)
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
    public void Configure()
    {
        SectorArray.ConfigureSectorNodes(_fieldTileAmount, _sectorTileAmount);
        WindowArray.ConfigureWindowNodes(SectorArray.Nodes, _costs, _portalPerWindow, _sectorMatrixSize, _fieldTileAmount);
        SectorArray.ConfigureSectorToWindowPoiners(WindowArray.Nodes);
        WindowArray.ConfigureWindowToSectorPointers(SectorArray.Nodes);
        PortalArray.ConfigurePortalNodes(WindowArray.Nodes, _costs, _fieldTileAmount, _portalPerWindow * 8 - 2);
        PortalArray.ConfigurePortalToPortalPtrs(_aStarGrid, SectorArray, WindowArray, _costs, _directions, _fieldTileAmount);
    }
    public WindowNode[] GetWindowNodesOf(SectorNode sectorNode)
    {
        WindowNode[] windowNodes = new WindowNode[sectorNode.SecToWinCnt];
        for(int i = sectorNode.SecToWinPtr; i < sectorNode.SecToWinPtr + sectorNode.SecToWinCnt; i++)
        {
            windowNodes[i - sectorNode.SecToWinPtr] = WindowArray.Nodes[SectorArray.WinPtrs[i]];
        }
        return windowNodes;
    }
    public SectorNode[] GetSectorNodesOf(WindowNode windowNode)
    {
        SectorNode[] sectorNodes = new SectorNode[windowNode.WinToSecCnt];
        for (int i = windowNode.WinToSecPtr; i < windowNode.WinToSecPtr + windowNode.WinToSecCnt; i++)
        {
            sectorNodes[i - windowNode.WinToSecPtr] = SectorArray.Nodes[WindowArray.SecPtrs[i]];
        }
        return sectorNodes;
    }
    public SectorNode[] GetSectorNodesOf(PortalNode portal)
    {
        WindowNode windowNode = WindowArray.Nodes[portal.WinPtr];
        SectorNode[] sectorNodes = new SectorNode[2];

        for (int i = windowNode.WinToSecPtr; i < windowNode.WinToSecPtr + windowNode.WinToSecCnt; i++)
        {
            sectorNodes[i - windowNode.WinToSecPtr] = SectorArray.Nodes[WindowArray.SecPtrs[i]];
        }
        return sectorNodes;

    }
    public SectorNode GetSectorNodeAt(Vector3 pos)
    {
        float sectorSize = _sectorTileAmount * _fieldTileSize;
        Index2 index2 = new Index2(Mathf.FloorToInt(pos.z / sectorSize), Mathf.FloorToInt(pos.x / sectorSize));
        int index = Index2.ToIndex(index2, _sectorTileAmount);
        return SectorArray.Nodes[index];
    }
    
    public void ConfigureSectorNodes() => SectorArray.ConfigureSectorNodes(_fieldTileAmount, _sectorTileAmount);
    public void ConfigureWindowNodes() => WindowArray.ConfigureWindowNodes(SectorArray.Nodes, _costs, _portalPerWindow, _sectorMatrixSize, _fieldTileAmount);
    public void ConfigureSectorToWindowPoiners() => SectorArray.ConfigureSectorToWindowPoiners(WindowArray.Nodes);
    public void ConfigureWindowToSectorPointers() => WindowArray.ConfigureWindowToSectorPointers(SectorArray.Nodes);
    public void ConfigurePortalNodes() => PortalArray.ConfigurePortalNodes(WindowArray.Nodes, _costs, _fieldTileAmount, _portalPerWindow * 8 - 2);
    public void ConfigurePortalToPortalPtrs() => PortalArray.ConfigurePortalToPortalPtrs(_aStarGrid, SectorArray, WindowArray, _costs, _directions, _fieldTileAmount);
}
[BurstCompile]
public struct FieldGraphJob : IJob
{
    public FieldGraph FieldGraph;

    public void Execute()
    {
        FieldGraph.Configure();
    }
}

