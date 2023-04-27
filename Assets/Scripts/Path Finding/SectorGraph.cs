using System;
using System.Drawing;
using System.Numerics;
using Unity.Burst;
using Unity.Collections;
using UnityEngine;
public struct SectorGraph
{
    public NativeArray<SectorNode> SectorNodes;
    public NativeArray<WindowNode> WindowNodes;

    NativeArray<int> SectorToWindowPointers;
    NativeArray<int> WindowToSectorPointers;

    public SectorGraph(int sectorSize, int totalTileAmount)
    {
        int sectorMatrixSize = totalTileAmount / sectorSize;
        int sectorTotalSize = sectorMatrixSize * sectorMatrixSize;

        //innitialize fields
        SectorNodes = new NativeArray<SectorNode>(sectorTotalSize, Allocator.Persistent);
        WindowNodes = new NativeArray<WindowNode>(sectorMatrixSize * ((sectorMatrixSize - 1) * 2), Allocator.Persistent);
        WindowToSectorPointers = new NativeArray<int>(WindowNodes.Length * 2, Allocator.Persistent);
        SectorToWindowPointers = new NativeArray<int>(WindowNodes.Length * 2, Allocator.Persistent);

        //configuring fields
        ConfigureSectorNodesFor(ref SectorNodes);
        ConfigureWindowNodesFor(ref WindowNodes, ref SectorNodes);
        ConfigureSectorToWindowPoiners(ref SectorNodes, ref WindowNodes, ref SectorToWindowPointers);
        ConfigureWindowToSectorPointers(ref SectorNodes, ref WindowNodes, ref WindowToSectorPointers);

        //HELPERS
        void ConfigureSectorNodesFor(ref NativeArray<SectorNode> sectorNodes)
        {
            int sectorMatrixSize = totalTileAmount / sectorSize;
            int sectorTotalSize = sectorMatrixSize * sectorMatrixSize;

            sectorNodes = new NativeArray<SectorNode>(sectorTotalSize, Allocator.Persistent);
            int iterableSecToWinPtr = 0;
            for (int r = 0; r < sectorMatrixSize; r++)
            {
                for (int c = 0; c < sectorMatrixSize; c++)
                {
                    int index = r * sectorMatrixSize + c;
                    Sector sect = new Sector(new Index2(r * sectorSize, c * sectorSize), sectorSize);
                    int secToWinCnt = 4;
                    if (sect.IsOnCorner(totalTileAmount))
                    {
                        secToWinCnt = 2;
                    }
                    else if (sect.IsOnEdge(totalTileAmount))
                    {
                        secToWinCnt = 3;
                    }
                    sectorNodes[index] = new SectorNode(sect, secToWinCnt, iterableSecToWinPtr);
                    iterableSecToWinPtr += secToWinCnt;
                }
            }
        }
        void ConfigureWindowNodesFor(ref NativeArray<WindowNode> windowNodes, ref NativeArray<SectorNode> helperSectorNodes)
        {
            int windowNodesIndex = 0;
            int iterableWinToSecPtr = 0;
            for (int r = 0; r < sectorMatrixSize; r++)
            {
                for (int c = 0; c < sectorMatrixSize; c++)
                {
                    int index = r * sectorMatrixSize + c;
                    Sector sector = helperSectorNodes[index].Sector;

                    //create upper window relative to the sector
                    if (!sector.IsOnTop(totalTileAmount))
                    {
                        windowNodes[windowNodesIndex] = new WindowNode(GetUpperWindowFor(sector), 2, iterableWinToSecPtr);
                        windowNodesIndex++;
                        iterableWinToSecPtr += 2;
                    }

                    //create right window relative to the sector
                    if (!sector.IsOnRight(totalTileAmount))
                    {
                        windowNodes[windowNodesIndex] = new WindowNode(GetRightWindowFor(sector), 2, iterableWinToSecPtr);
                        windowNodesIndex++;
                        iterableWinToSecPtr += 2;
                    }

                }
            }
            Window GetUpperWindowFor(Sector sector)
            {
                Index2 bottomLeftBoundary = new Index2(sector.StartIndex.R + sector.Size - 1, sector.StartIndex.C);
                Index2 topRightBoundary = new Index2(sector.StartIndex.R + sector.Size, sector.StartIndex.C + sector.Size - 1);
                return new Window(bottomLeftBoundary, topRightBoundary);
            }
            Window GetRightWindowFor(Sector sector)
            {
                Index2 bottomLeftBoundary = new Index2(sector.StartIndex.R, sector.StartIndex.C + sector.Size - 1);
                Index2 topRightBoundary = new Index2(bottomLeftBoundary.R + sector.Size - 1, bottomLeftBoundary.C + 1);
                return new Window(bottomLeftBoundary, topRightBoundary);
            }
        }       
        void ConfigureSectorToWindowPoiners(ref NativeArray<SectorNode> sectorNodes, ref NativeArray<WindowNode> windowNodes, ref NativeArray<int> secToWınPointers)
        {
            int sectorSize = sectorNodes[0].Sector.Size;
            int secToWinPtrIterable = 0;
            for(int i = 0; i < sectorNodes.Length; i++)
            {
                Index2 sectorStartIndex = sectorNodes[i].Sector.StartIndex;
                Index2 topWinIndex = new Index2(sectorStartIndex.R + sectorSize - 1, sectorStartIndex.C);
                Index2 rightWinIndex = new Index2(sectorStartIndex.R, sectorStartIndex.C + sectorSize - 1);
                Index2 botWinIndex = new Index2(sectorStartIndex.R - 1, sectorStartIndex.C);
                Index2 leftWinIndex = new Index2(sectorStartIndex.R, sectorStartIndex.C - 1);
                for (int j = 0; j < windowNodes.Length; j++)
                {
                    Window window = windowNodes[j].Window;
                    if(window.BottomLeftBoundary == topWinIndex) { secToWınPointers[secToWinPtrIterable++] = j; }
                    else if(window.BottomLeftBoundary == rightWinIndex) { secToWınPointers[secToWinPtrIterable++] = j; }
                    else if(window.BottomLeftBoundary == botWinIndex) { secToWınPointers[secToWinPtrIterable++] = j; }
                    else if(window.BottomLeftBoundary == leftWinIndex) { secToWınPointers[secToWinPtrIterable++] = j; }
                }
            }
        }
        void ConfigureWindowToSectorPointers(ref NativeArray<SectorNode> sectorNodes, ref NativeArray<WindowNode> windowNodes, ref NativeArray<int> winToSecPointers)
        {
            int winToSecPtrIterable = 0;
            for (int i = 0; i < windowNodes.Length; i++)
            {
                Index2 botLeft = windowNodes[i].Window.BottomLeftBoundary;
                Index2 topRight = windowNodes[i].Window.TopRightBoundary;
                for(int j = 0; j < sectorNodes.Length; j++)
                {
                    if (sectorNodes[j].Sector.ContainsIndex(botLeft))
                    {
                        winToSecPointers[winToSecPtrIterable++] = j;
                    }
                    else if (sectorNodes[j].Sector.ContainsIndex(topRight))
                    {
                        winToSecPointers[winToSecPtrIterable++] = j;
                    }
                }
            }
        }
    }
    public WindowNode[] GetWindowNodesOf(SectorNode sectorNode)
    {
        WindowNode[] windowNodes = new WindowNode[sectorNode.SecToWinCnt];
        for(int i = sectorNode.SecToWinPtr; i < sectorNode.SecToWinPtr + sectorNode.SecToWinCnt; i++)
        {
            windowNodes[i - sectorNode.SecToWinPtr] = WindowNodes[SectorToWindowPointers[i]];
        }
        return windowNodes;
    }
    public SectorNode[] GetSectorNodesOf(WindowNode windowNode)
    {
        SectorNode[] sectorNodes = new SectorNode[windowNode.WinToSecCnt];
        for (int i = windowNode.WinToSecPtr; i < windowNode.WinToSecPtr + windowNode.WinToSecCnt; i++)
        {
            sectorNodes[i - windowNode.WinToSecPtr] = SectorNodes[WindowToSectorPointers[i]];
        }
        return sectorNodes;
    }
}
public struct SectorNode
{
    public Sector Sector;
    public int SecToWinPtr;
    public int SecToWinCnt;

    public SectorNode(Sector sector, int secToWinCnt, int secToWinPtr)
    {
        Sector = sector;
        SecToWinCnt = secToWinCnt;
        SecToWinPtr = secToWinPtr;
    }
}
public struct WindowNode
{
    public Window Window;
    public int WinToSecPtr;
    public int WinToSecCnt;

    public WindowNode(Window window, int winToSecCnt, int winToSecPtr)
    {
        Window = window;
        WinToSecCnt = winToSecCnt;
        WinToSecPtr = winToSecPtr;
    }
}
public struct Sector
{
    public Index2 StartIndex;
    public int Size;

    public Sector(Index2 startIndex, int size)
    {
        StartIndex = startIndex;
        Size = size;
    }
    public bool ContainsIndex(Index2 index)
    {
        if(index.R < StartIndex.R) { return false; }
        if(index.C < StartIndex.C) { return false; }
        if(index.R >= StartIndex.R + Size) { return false; }
        if(index.C >= StartIndex.C + Size) { return false; }
        return true;
    }
    public bool IsOnCorner(int matrixSize) => (IsOnTop(matrixSize) && IsOnRight(matrixSize)) || (IsOnTop(matrixSize) && IsOnLeft()) || (IsOnBottom() && IsOnRight(matrixSize)) || (IsOnBottom() && IsOnLeft());
    public bool IsOnEdge(int matrixSize) => IsOnTop(matrixSize) || IsOnBottom() || IsOnRight(matrixSize) || IsOnLeft();
    public bool IsOnTop(int matrixSize) => StartIndex.R + Size >= matrixSize;
    public bool IsOnBottom() => StartIndex.R == 0;
    public bool IsOnRight(int matrixSize) => StartIndex.C + Size >= matrixSize;
    public bool IsOnLeft() => StartIndex.C == 0;
    public static bool operator == (Sector sector1, Sector sector2)
    {
        return sector1.StartIndex == sector2.StartIndex;
    }
    public static bool operator != (Sector sector1, Sector sector2)
    {
        return sector1.StartIndex != sector2.StartIndex;
    }
}
public struct Window
{
    public Index2 BottomLeftBoundary;
    public Index2 TopRightBoundary;

    public Window(Index2 bottomLeftBoundary, Index2 topRightBoundary)
    {
        BottomLeftBoundary = bottomLeftBoundary;
        TopRightBoundary = topRightBoundary;
    }
}
