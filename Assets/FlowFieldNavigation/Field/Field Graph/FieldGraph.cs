using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Unity.Mathematics;
using System;
using Unity.Collections.LowLevel.Unsafe;

internal class FieldGraph
{
    //main graph elements
    internal NativeArray<SectorNode> SectorNodes;
    internal NativeArray<int> SecToWinPtrs;
    internal NativeArray<WindowNode> WindowNodes;
    internal NativeArray<int> WinToSecPtrs;
    internal NativeArray<PortalNode> PortalNodes;
    internal NativeArray<PortalToPortal> PorToPorPtrs;
    internal NativeList<IslandData> IslandDataList;
    internal NativeArray<UnsafeList<int>> IslandFields;
    internal SectorBitArray EditedSectorMarks;
    internal NativeArray<AStarTile> SectorIntegrationField;

    //helper data
    internal NativeList<int> EditedSectorList;
    internal NativeBitArray EditedWindowMarks;
    internal NativeList<int> EditedWinodwList;
    internal NativeList<CostEdit> NewCostEdits;

    internal int PortalPerWindow;
    internal FieldGraph(int costFieldOffset)
    {
        //size calculations
        int sectorMatrixRowAmount = FlowFieldUtilities.SectorMatrixRowAmount;
        int sectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount;

        int sectorAmount = sectorMatrixRowAmount * sectorMatrixColAmount;
        int windowAmount = 2 * sectorMatrixColAmount * sectorMatrixRowAmount - sectorMatrixRowAmount - sectorMatrixColAmount;
        int winToSecPtrAmount = windowAmount * 2;
        int secToWinPtrAmount = windowAmount * 2;
        int portalPerWindow = GetPortalPerWindow(costFieldOffset);
        int portalAmount = windowAmount * portalPerWindow;
        int porToPorPtrAmount = portalAmount * (portalPerWindow * 8 - 2);

        //innitialize fields
        PortalPerWindow = portalPerWindow;
        SectorNodes = new NativeArray<SectorNode>(sectorAmount, Allocator.Persistent);
        SecToWinPtrs = new NativeArray<int>(secToWinPtrAmount, Allocator.Persistent);
        WindowNodes = new NativeArray<WindowNode>(windowAmount, Allocator.Persistent);
        WinToSecPtrs = new NativeArray<int>(winToSecPtrAmount, Allocator.Persistent);
        PortalNodes = new NativeArray<PortalNode>(portalAmount + 1, Allocator.Persistent);
        PorToPorPtrs = new NativeArray<PortalToPortal>(porToPorPtrAmount, Allocator.Persistent);
        IslandDataList = new NativeList<IslandData>(Allocator.Persistent);
        IslandDataList.Length = 1;
        IslandFields = new NativeArray<UnsafeList<int>>(FlowFieldUtilities.SectorMatrixTileAmount, Allocator.Persistent);
        for(int i = 0; i < IslandFields.Length; i++)
        {
            IslandFields[i] = new UnsafeList<int>(0, Allocator.Persistent);
        }
        EditedSectorList = new NativeList<int>(Allocator.Persistent);
        EditedSectorMarks = new SectorBitArray(FlowFieldUtilities.SectorMatrixTileAmount, Allocator.Persistent);
        NewCostEdits = new NativeList<CostEdit>(Allocator.Persistent);
        EditedWindowMarks = new NativeBitArray(WindowNodes.Length, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        EditedWinodwList = new NativeList<int>(Allocator.Persistent);
        SectorIntegrationField = new NativeArray<AStarTile>(FlowFieldUtilities.SectorTileAmount, Allocator.Persistent);
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

    internal FieldGraphConfigurationJob GetConfigJob(NativeArray<byte> costs)
    {
        return new FieldGraphConfigurationJob()
        {
            SectorColAmount = FlowFieldUtilities.SectorColAmount,
            FieldColAmount = FlowFieldUtilities.FieldColAmount,
            FieldRowAmount = FlowFieldUtilities.FieldRowAmount,
            SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
            SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
            SectorMatrixRowAmount = FlowFieldUtilities.SectorMatrixRowAmount,
            PortalPerWindow = PortalPerWindow,
            SectorRowAmount = FlowFieldUtilities.SectorRowAmount,
            Costs = costs,
            SectorNodes = SectorNodes,
            SecToWinPtrs = SecToWinPtrs,
            WindowNodes = WindowNodes,
            WinToSecPtrs = WinToSecPtrs,
            PortalNodes = PortalNodes,
            PorToPorPtrs = PorToPorPtrs,
            IntegratedCosts = SectorIntegrationField,
        };
    }
    internal IslandConfigurationJob GetIslandConfigJob(NativeArray<byte> costs)
    {
        return new IslandConfigurationJob()
        {
            SectorColAmount = FlowFieldUtilities.SectorColAmount,
            SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
            SectorTileAmount = FlowFieldUtilities.SectorColAmount * FlowFieldUtilities.SectorRowAmount,
            IslandFields = IslandFields,
            CostsL = costs,
            SectorNodes = SectorNodes,
            SecToWinPtrs = SecToWinPtrs,
            Islands = IslandDataList,
            PortalEdges = PorToPorPtrs,
            PortalNodes = PortalNodes,
            WindowNodes = WindowNodes
        };
    }
    internal IslandFieldProcessor GetIslandFieldProcessor()
    {
        return new IslandFieldProcessor()
        {
            SectorColAmount = FlowFieldUtilities.SectorColAmount,
            SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
            TileSize = FlowFieldUtilities.TileSize,
            FieldColAmount = FlowFieldUtilities.FieldColAmount,
            SectorNodes = FlowFieldUtilitiesUnsafe.ToUnsafeListRedonly(SectorNodes),
            IslandFields = FlowFieldUtilitiesUnsafe.ToUnsafeListRedonly(IslandFields),
            PortalNodes = FlowFieldUtilitiesUnsafe.ToUnsafeListRedonly(PortalNodes),
        };
    }
    internal NativeArray<WindowNode> GetWindowNodesOf(SectorNode sectorNode)
    {
        NativeArray <WindowNode> windowNodes = new NativeArray<WindowNode>(sectorNode.SecToWinCnt, Allocator.Temp);
        for(int i = sectorNode.SecToWinPtr; i < sectorNode.SecToWinPtr + sectorNode.SecToWinCnt; i++)
        {
            windowNodes[i - sectorNode.SecToWinPtr] = WindowNodes[SecToWinPtrs[i]];
        }
        return windowNodes;
    }
    internal NativeArray<SectorNode> GetSectorNodesOf(WindowNode windowNode)
    {
        NativeArray<SectorNode> sectorNodes = new NativeArray<SectorNode>(windowNode.WinToSecCnt, Allocator.Temp);
        for (int i = windowNode.WinToSecPtr; i < windowNode.WinToSecPtr + windowNode.WinToSecCnt; i++)
        {
            sectorNodes[i - windowNode.WinToSecPtr] = SectorNodes[WinToSecPtrs[i]];
        }
        return sectorNodes;
    }
    internal NativeArray<SectorNode> GetSectorNodesOf(PortalNode portal)
    {
        WindowNode windowNode = WindowNodes[portal.WinPtr];
        NativeArray<SectorNode> sectorNodes = new NativeArray<SectorNode>(2, Allocator.Temp);

        for (int i = windowNode.WinToSecPtr; i < windowNode.WinToSecPtr + windowNode.WinToSecCnt; i++)
        {
            sectorNodes[i - windowNode.WinToSecPtr] = SectorNodes[WinToSecPtrs[i]];
        }
        return sectorNodes;
    }
    internal SectorNode GetSectorNodeAt(Vector3 pos)
    {
        float sectorSize = FlowFieldUtilities.SectorColAmount * FlowFieldUtilities.TileSize;
        Index2 index2 = new Index2(Mathf.FloorToInt(pos.z / sectorSize), Mathf.FloorToInt(pos.x / sectorSize));
        int index = Index2.ToIndex(index2, FlowFieldUtilities.SectorMatrixColAmount);
        return SectorNodes[index];
    }
    internal NativeArray<int> GetPortalIndicies(SectorNode sectorNode, NativeArray<WindowNode> windowNodes)
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

internal enum IslandData : byte
{
    Removed,
    Dirty,
    Clean,
};