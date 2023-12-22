using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Unity.Mathematics;
using System;
using Unity.Collections.LowLevel.Unsafe;

public class FieldGraph
{
    //main graph elements
    public UnsafeList<SectorNode> SectorNodes;
    public NativeArray<int> SecToWinPtrs;
    public NativeArray<WindowNode> WindowNodes;
    public NativeArray<int> WinToSecPtrs;
    public UnsafeList<PortalNode> PortalNodes;
    public NativeArray<PortalToPortal> PorToPorPtrs;
    public NativeList<IslandData> IslandDataList;
    public UnsafeList<UnsafeList<int>> IslandFields;
    public SectorBitArray EditedSectorMarks;
    public AStarGrid _aStarGrid;

    //helper data
    public NativeArray<byte> _costsL;
    public UnsafeList<byte> _costsG;
    public NativeList<int> EditedSectorList;
    public NativeBitArray EditedWindowMarks;
    public NativeList<int> EditedWinodwList;
    public NativeList<Obstacle> NewObstacles;

    float _tileSize;
    int _fieldRowAmount;
    int _fieldColAmount;
    int _sectorTileAmount;
    int _sectorMatrixRowAmount;
    int _sectorMatrixColAmount;
    public int PortalPerWindow;
    public FieldGraph(UnsafeList<byte> costsG, NativeArray<byte> costsL, int sectorSize, int fieldRowAmount, int fieldColAmount, int costFieldOffset, float tileSize)
    {
        //size calculations
        int sectorMatrixRowAmount = fieldRowAmount / sectorSize;
        int sectorMatrixColAmount = fieldColAmount / sectorSize;

        int sectorAmount = sectorMatrixRowAmount * sectorMatrixColAmount;
        int windowAmount = 2 * sectorMatrixColAmount * sectorMatrixRowAmount - sectorMatrixRowAmount - sectorMatrixColAmount;
        int winToSecPtrAmount = windowAmount * 2;
        int secToWinPtrAmount = windowAmount * 2;
        int portalPerWindow = GetPortalPerWindow(costFieldOffset);
        int portalAmount = windowAmount * portalPerWindow;
        int porToPorPtrAmount = portalAmount * (portalPerWindow * 8 - 2);

        //innitialize fields
        _tileSize = tileSize;
        _sectorMatrixColAmount = sectorMatrixColAmount;
        _sectorMatrixRowAmount = sectorMatrixRowAmount;
        _fieldColAmount = fieldColAmount;
        _fieldRowAmount = fieldRowAmount;
        _sectorTileAmount = sectorSize;
        PortalPerWindow = portalPerWindow;
        _costsG = costsG;
        _costsL = costsL;
        _aStarGrid = new AStarGrid(fieldRowAmount, fieldColAmount);
        SectorNodes = new UnsafeList<SectorNode>(sectorAmount, Allocator.Persistent);
        SectorNodes.Length = sectorAmount;
        SecToWinPtrs = new NativeArray<int>(secToWinPtrAmount, Allocator.Persistent);
        WindowNodes = new NativeArray<WindowNode>(windowAmount, Allocator.Persistent);
        WinToSecPtrs = new NativeArray<int>(winToSecPtrAmount, Allocator.Persistent);
        PortalNodes = new UnsafeList<PortalNode>(portalAmount + 1, Allocator.Persistent);
        PortalNodes.Length = portalAmount;
        PorToPorPtrs = new NativeArray<PortalToPortal>(porToPorPtrAmount, Allocator.Persistent);
        IslandDataList = new NativeList<IslandData>(Allocator.Persistent);
        IslandDataList.Length = 1;
        IslandFields = new UnsafeList<UnsafeList<int>>(_sectorMatrixColAmount * _sectorMatrixRowAmount, Allocator.Persistent);
        IslandFields.Length = _sectorMatrixColAmount * _sectorMatrixRowAmount;
        for(int i = 0; i < IslandFields.Length; i++)
        {
            IslandFields[i] = new UnsafeList<int>(0, Allocator.Persistent);
        }
        EditedSectorList = new NativeList<int>(Allocator.Persistent);
        EditedSectorMarks = new SectorBitArray(FlowFieldUtilities.SectorMatrixTileAmount, Allocator.Persistent);
        NewObstacles = new NativeList<Obstacle>(Allocator.Persistent);
        EditedWindowMarks = new NativeBitArray(WindowNodes.Length, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        EditedWinodwList = new NativeList<int>(Allocator.Persistent);

        int GetPortalPerWindow(int offset)
        {
            switch (offset)
            {
                case 0:
                    return 5;
                case 1:
                    return 3;
                case 2:
                    return 2;
                case 3:
                    return 2;
                case > 3:
                    return 1;
            }
            return -1;
        }
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
            PortalPerWindow = PortalPerWindow,
            IntegratedCosts = _aStarGrid._integratedCosts,
            AStarQueue = _aStarGrid._searchQueue,
        };
    }
    public IslandConfigurationJob GetIslandConfigJob()
    {
        return new IslandConfigurationJob()
        {
            SectorColAmount = FlowFieldUtilities.SectorColAmount,
            SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
            SectorTileAmount = FlowFieldUtilities.SectorColAmount * FlowFieldUtilities.SectorRowAmount,
            IslandFields = IslandFields,
            CostsL = _costsL,
            SectorNodes = SectorNodes,
            SecToWinPtrs = SecToWinPtrs,
            Islands = IslandDataList,
            PortalEdges = PorToPorPtrs,
            PortalNodes = PortalNodes,
            WindowNodes = WindowNodes
        };
    }
    public IslandFieldProcessor GetIslandFieldProcessor()
    {
        return new IslandFieldProcessor()
        {
            SectorColAmount = FlowFieldUtilities.SectorColAmount,
            SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
            TileSize = FlowFieldUtilities.TileSize,
            FieldColAmount = FlowFieldUtilities.FieldColAmount,
            SectorNodes = SectorNodes,
            IslandFields = IslandFields,
            PortalNodes = PortalNodes,
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

public enum IslandData : byte
{
    Removed,
    Dirty,
    Clean,
};