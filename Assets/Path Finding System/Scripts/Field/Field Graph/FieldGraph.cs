using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Unity.Mathematics;
using System;
using Unity.Collections.LowLevel.Unsafe;

public struct FieldGraph
{
    //main graph elements
    public NativeArray<SectorNode> SectorNodes;
    public NativeArray<int> SecToWinPtrs;
    public NativeArray<WindowNode> WindowNodes;
    public NativeArray<int> WinToSecPtrs;
    public NativeArray<PortalNode> PortalNodes;
    public NativeArray<PortalToPortal> PorToPorPtrs;
    AStarGrid _aStarGrid;

    //helper data
    NativeArray<UnsafeList<byte>> _costsL;
    UnsafeList<byte> _costsG;
    float _tileSize;
    int _fieldRowAmount;
    int _fieldColAmount;
    int _sectorTileAmount;
    int _sectorMatrixRowAmount;
    int _sectorMatrixColAmount;
    int _portalPerWindow;
    public FieldGraph(UnsafeList<byte> costsG, NativeArray<UnsafeList<byte>> costsL, int sectorSize, int fieldRowAmount, int fieldColAmount, int costFieldOffset, float tileSize)
    {
        //size calculations
        int sectorMatrixRowAmount = fieldRowAmount / sectorSize;
        int sectorMatrixColAmount = fieldColAmount / sectorSize;

        int sectorAmount = sectorMatrixRowAmount * sectorMatrixColAmount;
        int windowAmount = 2 * sectorMatrixColAmount * sectorMatrixRowAmount - sectorMatrixRowAmount - sectorMatrixColAmount;
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
        _tileSize = tileSize;
        _sectorMatrixColAmount = sectorMatrixColAmount;
        _sectorMatrixRowAmount = sectorMatrixRowAmount;
        _fieldColAmount = fieldColAmount;
        _fieldRowAmount = fieldRowAmount;
        _sectorTileAmount = sectorSize;
        _portalPerWindow = portalPerWindow;
        _costsG = costsG;
        _costsL = costsL;
        _aStarGrid = new AStarGrid(fieldRowAmount, fieldColAmount);
        SectorNodes = new NativeArray<SectorNode>(sectorAmount, Allocator.Persistent);
        SecToWinPtrs = new NativeArray<int>(secToWinPtrAmount, Allocator.Persistent);
        WindowNodes = new NativeArray<WindowNode>(windowAmount, Allocator.Persistent);
        WinToSecPtrs = new NativeArray<int>(winToSecPtrAmount, Allocator.Persistent);
        PortalNodes = new NativeArray<PortalNode>(portalAmount + 1, Allocator.Persistent);
        PorToPorPtrs = new NativeArray<PortalToPortal>(porToPorPtrAmount, Allocator.Persistent);
    }
    public FieldGraphConfigurationJob GetConfigJob()
    {
        return new FieldGraphConfigurationJob()
        {
            SectorNodes = SectorNodes,
            SecToWinPtrs = SecToWinPtrs,
            WindowNodes = WindowNodes,
            WinToSecPtrs = WinToSecPtrs,
            PortalNodes = PortalNodes,
            PorToPorPtrs = PorToPorPtrs,
            Costs = _costsG,
            FieldColAmount = _fieldColAmount,
            FieldRowAmount = _fieldRowAmount,
            SectorTileAmount = _sectorTileAmount,
            SectorMatrixColAmount = _sectorMatrixColAmount,
            SectorMatrixRowAmount = _sectorMatrixRowAmount,
            PortalPerWindow = _portalPerWindow,
            IntegratedCosts = _aStarGrid._integratedCosts,
            AStarQueue = _aStarGrid._searchQueue,
        };
    }
    public CostFieldEditJob GetEditJob(BoundaryData bounds, byte newCost)
    {
        return new CostFieldEditJob()
        {
            NewCost = newCost,
            Bounds = bounds,
            SectorNodes = SectorNodes,
            SecToWinPtrs = SecToWinPtrs,
            WindowNodes = WindowNodes,
            WinToSecPtrs = WinToSecPtrs,
            PortalNodes = PortalNodes,
            PorPtrs = PorToPorPtrs,
            CostsL = _costsL,
            CostsG = _costsG,
            FieldColAmount = _fieldColAmount,
            FieldRowAmount = _fieldRowAmount,
            SectorColAmount = _sectorTileAmount,
            SectorMatrixColAmount = _sectorMatrixColAmount,
            SectorMatrixRowAmount = _sectorMatrixRowAmount,
            PortalPerWindow = _portalPerWindow,
            IntegratedCosts = _aStarGrid._integratedCosts,
            AStarQueue = _aStarGrid._searchQueue,
            EditedSectorIndicies = new NativeList<int>(Allocator.Persistent)
        };
    }
    public NativeArray<WindowNode> GetWindowNodesOf(SectorNode sectorNode)
    {
        NativeArray <WindowNode> windowNodes = new NativeArray<WindowNode>(sectorNode.SecToWinCnt, Allocator.Temp);
        for(int i = sectorNode.SecToWinPtr; i < sectorNode.SecToWinPtr + sectorNode.SecToWinCnt; i++)
        {
            windowNodes[i - sectorNode.SecToWinPtr] = WindowNodes[SecToWinPtrs[i]];
        }
        return windowNodes;
    }
    public NativeArray<SectorNode> GetSectorNodesOf(WindowNode windowNode)
    {
        NativeArray<SectorNode> sectorNodes = new NativeArray<SectorNode>(windowNode.WinToSecCnt, Allocator.Temp);
        for (int i = windowNode.WinToSecPtr; i < windowNode.WinToSecPtr + windowNode.WinToSecCnt; i++)
        {
            sectorNodes[i - windowNode.WinToSecPtr] = SectorNodes[WinToSecPtrs[i]];
        }
        return sectorNodes;
    }
    public NativeArray<SectorNode> GetSectorNodesOf(PortalNode portal)
    {
        WindowNode windowNode = WindowNodes[portal.WinPtr];
        NativeArray<SectorNode> sectorNodes = new NativeArray<SectorNode>(2, Allocator.Temp);

        for (int i = windowNode.WinToSecPtr; i < windowNode.WinToSecPtr + windowNode.WinToSecCnt; i++)
        {
            sectorNodes[i - windowNode.WinToSecPtr] = SectorNodes[WinToSecPtrs[i]];
        }
        return sectorNodes;
    }
    public NativeArray<int> GetWindowNodeIndiciesOf(SectorNode sectorNode)
    {
        NativeArray<int> windowNodeIndicies = new NativeArray<int>(sectorNode.SecToWinCnt, Allocator.Temp);
        for (int i = sectorNode.SecToWinPtr; i < sectorNode.SecToWinPtr + sectorNode.SecToWinCnt; i++)
        {
            windowNodeIndicies[i - sectorNode.SecToWinPtr] = SecToWinPtrs[i];
        }
        return windowNodeIndicies;
    }
    public NativeArray<int> GetSectorNodeIndiciesOf(WindowNode windowNode)
    {
        NativeArray<int> sectorNodeIndicies = new NativeArray<int>(windowNode.WinToSecCnt, Allocator.Temp);
        for (int i = windowNode.WinToSecPtr; i < windowNode.WinToSecPtr + windowNode.WinToSecCnt; i++)
        {
            sectorNodeIndicies[i - windowNode.WinToSecPtr] = WinToSecPtrs[i];
        }
        return sectorNodeIndicies;
    }
    public NativeArray<int> GetSectorNodeIndiciesOf(PortalNode portal)
    {
        WindowNode windowNodeIndex = WindowNodes[portal.WinPtr];
        NativeArray<int> sectorNodeIndicies = new NativeArray<int>(2, Allocator.Temp);

        for (int i = windowNodeIndex.WinToSecPtr; i < windowNodeIndex.WinToSecPtr + windowNodeIndex.WinToSecCnt; i++)
        {
            sectorNodeIndicies[i - windowNodeIndex.WinToSecPtr] = WinToSecPtrs[i];
        }
        return sectorNodeIndicies;
    }
    public SectorNode GetSectorNodeAt(Vector3 pos)
    {
        float sectorSize = _sectorTileAmount * _tileSize;
        Index2 index2 = new Index2(Mathf.FloorToInt(pos.z / sectorSize), Mathf.FloorToInt(pos.x / sectorSize));
        int index = Index2.ToIndex(index2, _sectorMatrixColAmount);
        return SectorNodes[index];
    }
    public NativeArray<int> GetPortalIndicies(SectorNode sectorNode, NativeArray<WindowNode> windowNodes)
    {
        NativeArray<int> portalIndicies;
        int secToWinCnt = sectorNode.SecToWinCnt;
        int secToWinPtr = sectorNode.SecToWinPtr;

        //determine portal count
        int portalIndexCount = 0;
        for (int i = 0; i < secToWinCnt; i++)
        {
            portalIndexCount += windowNodes[SecToWinPtrs[secToWinPtr + i]].PorCnt;
        }
        portalIndicies = new NativeArray<int>(portalIndexCount, Allocator.Temp);

        //get portals
        int portalIndiciesIterable = 0;
        for (int i = 0; i < secToWinCnt; i++)
        {
            int windowPorPtr = windowNodes[SecToWinPtrs[secToWinPtr + i]].PorPtr;
            int windowPorCnt = windowNodes[SecToWinPtrs[secToWinPtr + i]].PorCnt;
            for (int j = 0; j < windowPorCnt; j++)
            {
                portalIndicies[portalIndiciesIterable++] = windowPorPtr + j;
            }
        }
        return portalIndicies;
    }
}
