using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.VisualScripting;

[BurstCompile]
public struct NewActivePortalSubmitJob : IJob
{
    public int2 TargetIndex2D;
    public int SectorTileAmount;
    public int SectorColAmount;
    public int SectorRowAmount;
    public int SectorMatrixColAmount;
    public int SectorMatrixRowAmount;
    public int FieldColAmount;

    [ReadOnly] public UnsafeList<PortalNode> PortalNodes;
    [ReadOnly] public NativeArray<WindowNode> WindowNodes;
    [ReadOnly] public NativeArray<int> WinToSecPtrs;
    [ReadOnly] public NativeArray<int> PickedToSectors;
    [ReadOnly] public UnsafeList<int> SectorToPicked;
    [ReadOnly] public NativeArray<ActivePortal> PortalSequence;
    [ReadOnly] public NativeArray<int> PortalSequenceBorders;

    public NativeArray<UnsafeList<ActiveWaveFront>> ActiveWaveFrontListArray;
    

    public void Execute()
    {
        for (int i = 0; i < PortalSequenceBorders.Length - 1; i++)
        {
            int start = PortalSequenceBorders[i];
            int end = PortalSequenceBorders[i + 1];
            //HANDLE PORTALS EXCEPT TARGET NEIGHBOUR
            for(int j = start; j < end - 2; j++)
            {
                ActivePortal curPortal = PortalSequence[j];
                ActivePortal nextPortal = PortalSequence[j + 1];
                int curPortalIndex = curPortal.Index;
                int nextPortalIndex = nextPortal.Index;
                int windowIndex1 = PortalNodes[curPortalIndex].WinPtr;
                int windowIndex2 = PortalNodes[nextPortalIndex].WinPtr;
                WindowNode winNode1 = WindowNodes[windowIndex1];
                WindowNode winNode2 = WindowNodes[windowIndex2];
                int curSec1Index = WinToSecPtrs[winNode1.WinToSecPtr];
                int curSec2Index = WinToSecPtrs[winNode1.WinToSecPtr + 1];
                int nextSec1Index = WinToSecPtrs[winNode2.WinToSecPtr];
                int nextSec2Index = WinToSecPtrs[winNode2.WinToSecPtr + 1];

                if (curSec1Index != nextSec1Index && curSec1Index != nextSec2Index && SectorToPicked[curSec1Index] != 0)
                {
                    int pickedSectorIndex = (SectorToPicked[curSec1Index] - 1) / SectorTileAmount;
                    UnsafeList<ActiveWaveFront> activePortals = ActiveWaveFrontListArray[pickedSectorIndex];
                    int activeLocalIndex = GetIndexOfPortalAtSector(PortalNodes[curPortalIndex], curSec1Index);
                    ActiveWaveFront newActiveWaveFront = new ActiveWaveFront(activeLocalIndex, curPortal.Distance);
                    activePortals.Add(newActiveWaveFront);
                    ActiveWaveFrontListArray[pickedSectorIndex] = activePortals;

                }
                else if (curSec2Index != nextSec1Index && curSec2Index != nextSec2Index && SectorToPicked[curSec2Index] != 0)
                {
                    int pickedSectorIndex = (SectorToPicked[curSec2Index] - 1) / SectorTileAmount;
                    UnsafeList<ActiveWaveFront> activePortals = ActiveWaveFrontListArray[pickedSectorIndex];
                    int activeLocalIndex = GetIndexOfPortalAtSector(PortalNodes[curPortalIndex], curSec2Index);
                    ActiveWaveFront newActiveWaveFront = new ActiveWaveFront(activeLocalIndex, curPortal.Distance);
                    activePortals.Add(newActiveWaveFront);
                    ActiveWaveFrontListArray[pickedSectorIndex] = activePortals;
                }
            }
            
            //HANDLE TARGET NEIGBOUR POINTING TOWARDS TARGET
            ActivePortal endPortal = PortalSequence[end - 2];
            if (endPortal.IsTargetNeighbour())
            {
                int endPortalIndex = endPortal.Index;
                int endPortalWinIndex = PortalNodes[endPortalIndex].WinPtr;
                WindowNode endPortalWinNode = WindowNodes[endPortalWinIndex];
                int endSector1 = WinToSecPtrs[endPortalWinNode.WinToSecPtr];
                int endSector2 = WinToSecPtrs[endPortalWinNode.WinToSecPtr + 1];
                int2 targetSector2d = FlowFieldUtilities.GetSectorIndex(TargetIndex2D, SectorColAmount);
                int targetSector1d = FlowFieldUtilities.To1D(targetSector2d, SectorMatrixColAmount);
                if(targetSector1d != endSector1 && SectorToPicked[endSector1] != 0)
                {
                    int pickedSectorIndex = (SectorToPicked[endSector1] - 1) / SectorTileAmount;
                    UnsafeList<ActiveWaveFront> activePortals = ActiveWaveFrontListArray[pickedSectorIndex];
                    int activeLocalIndex = GetIndexOfPortalAtSector(PortalNodes[endPortalIndex], endSector1);
                    ActiveWaveFront newActiveWaveFront = new ActiveWaveFront(activeLocalIndex, endPortal.Distance);
                    if(ActiveWaveFrontExists(newActiveWaveFront, activePortals)) { continue; }
                    activePortals.Add(newActiveWaveFront);
                    ActiveWaveFrontListArray[pickedSectorIndex] = activePortals;
                }
                else if(SectorToPicked[endSector2] != 0)
                {
                    int pickedSectorIndex = (SectorToPicked[endSector2] - 1) / SectorTileAmount;
                    UnsafeList<ActiveWaveFront> activePortals = ActiveWaveFrontListArray[pickedSectorIndex];
                    int activeLocalIndex = GetIndexOfPortalAtSector(PortalNodes[endPortalIndex], endSector2);
                    ActiveWaveFront newActiveWaveFront = new ActiveWaveFront(activeLocalIndex, endPortal.Distance);
                    if (ActiveWaveFrontExists(newActiveWaveFront, activePortals)) { continue; }
                    activePortals.Add(newActiveWaveFront);
                    ActiveWaveFrontListArray[pickedSectorIndex] = activePortals;
                }
            }
        }
        int2 targetSectorIndex2d = FlowFieldUtilities.GetSectorIndex(TargetIndex2D, SectorColAmount);
        int targetSectorIndex1d = FlowFieldUtilities.To1D(targetSectorIndex2d, SectorMatrixColAmount);
        int targetPickedSectorIndex = (SectorToPicked[targetSectorIndex1d] - 1) / SectorTileAmount;
        int2 targetSectorStartIndex2d = FlowFieldUtilities.GetSectorStartIndex(targetSectorIndex2d, SectorColAmount);
        int2 targetLocalIndex2d = FlowFieldUtilities.GetLocalIndex(TargetIndex2D, targetSectorStartIndex2d);
        int targetLocalIndex1d = FlowFieldUtilities.To1D(targetLocalIndex2d, SectorColAmount);
        UnsafeList<ActiveWaveFront> targetActivePortals = ActiveWaveFrontListArray[targetPickedSectorIndex];
        ActiveWaveFront targetFront = new ActiveWaveFront(targetLocalIndex1d, 0f);
        targetActivePortals.Add(targetFront);
        ActiveWaveFrontListArray[targetPickedSectorIndex] = targetActivePortals;
    }
    bool ActiveWaveFrontExists(ActiveWaveFront front, UnsafeList<ActiveWaveFront> list)
    {
        for(int i = 0; i < list.Length; i++)
        {
            ActiveWaveFront curFront = list[i];
            if (curFront.LocalIndex == front.LocalIndex) { return true; }
        }
        return false;
    }
    int GetIndexOfPortalAtSector(PortalNode portalNode, int sectorIndex)
    {
        int2 i1 = new int2(portalNode.Portal1.Index.C, portalNode.Portal1.Index.R);
        int2 i2 = new int2(portalNode.Portal2.Index.C, portalNode.Portal2.Index.R);
        int2 i1sector2d = FlowFieldUtilities.GetSectorIndex(i1, SectorColAmount);
        int2 i2sector2d = FlowFieldUtilities.GetSectorIndex(i2, SectorColAmount);
        
        int i1sector1d = FlowFieldUtilities.To1D(i1sector2d, SectorMatrixColAmount);
        
        int2 pickedIndex2d = math.select(i2, i1, i1sector1d == sectorIndex);
        int2 i1sectorStart2d = FlowFieldUtilities.GetSectorStartIndex(i1sector2d, SectorColAmount);
        int2 i2sectorStart2d = FlowFieldUtilities.GetSectorStartIndex(i2sector2d, SectorColAmount);
        int2 pickedSectorStart2d = math.select(i2sectorStart2d, i1sectorStart2d, i1sector1d == sectorIndex);
        
        int2 pickedIndexLocal2d = FlowFieldUtilities.GetLocalIndex(pickedIndex2d, pickedSectorStart2d);
        return FlowFieldUtilities.To1D(pickedIndexLocal2d, SectorColAmount);
    }
}
[BurstCompile]
public struct ActiveWaveFront
{
    public int LocalIndex;
    public float Distance;

    public ActiveWaveFront(int localIndes, float distance)
    {
        LocalIndex = localIndes;
        Distance = distance;
    }
}