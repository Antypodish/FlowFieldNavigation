using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using UnityEngine;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct FlowFieldAdditionTraversalJob : IJob
{
    public int SectorColAmount;
    public int SectorMatrixColAmount;
    public NativeArray<int> SourceSectorIndicies;
    [ReadOnly] public NativeArray<SectorNode> SectorNodes;
    [ReadOnly] public NativeArray<int> SecToWinPtrs;
    [ReadOnly] public NativeArray<WindowNode> WindowNodes;
    [ReadOnly] public NativeArray<int> WinToSecPtrs;
    [ReadOnly] public NativeArray<PortalNode> PortalNodes;
    [ReadOnly] public NativeArray<PortalToPortal> PorPtrs;
    public NativeArray<int> ConnectionIndicies;
    public NativeArray<PortalMark> PortalMarks;
    public NativeList<PortalSequence> PortalSequence;
    public NativeArray<int> SectorMarks;
    public NativeList<IntegrationFieldSector> IntegrationField;
    public NativeList<FlowFieldSector> FlowField;
    public NativeList<LocalIndex1d> IntegrationStartIndicies;
    int _newPortalSequenceStartIndex;
    public void Execute()
    {
        _newPortalSequenceStartIndex = PortalSequence.Length;
        StartGraphWalker();
    }
    void StartGraphWalker()
    {
        for (int i = 0; i < SourceSectorIndicies.Length; i++)
        {
            int sourceSectorIndex = SourceSectorIndicies[i];
            UnsafeList<int> sourcePortalIndicies = GetPortalIndicies(sourceSectorIndex);

            for (int j = 0; j < sourcePortalIndicies.Length; j++)
            {
                SetPortalSequence(sourcePortalIndicies[j]);
            }
        }
        PickSectorsFromNewPortalSequences();
        for(int i = _newPortalSequenceStartIndex; i < PortalSequence.Length; i++)
        {
            PortalMarks[PortalSequence[i].PortalPtr] = PortalMark.MainWalker;
        }
    }
    UnsafeList<int> GetPortalIndicies(int targetSectorIndexF)
    {
        UnsafeList<int> portalIndicies = new UnsafeList<int>(0, Allocator.Temp);
        SectorNode sectorNode = SectorNodes[targetSectorIndexF];
        int winPtr = sectorNode.SecToWinPtr;
        int winCnt = sectorNode.SecToWinCnt;
        for (int i = 0; i < winCnt; i++)
        {
            WindowNode windowNode = WindowNodes[SecToWinPtrs[winPtr + i]];
            int porPtr = windowNode.PorPtr;
            int porCnt = windowNode.PorCnt;
            for (int j = 0; j < porCnt; j++)
            {
                portalIndicies.Add(j + porPtr);
            }
        }
        return portalIndicies;
    }
    void SetPortalSequence(int startingPortalIndex)
    {
        NativeList<PortalSequence> portalSequence = PortalSequence;
        int cur = startingPortalIndex;
        int next = ConnectionIndicies[startingPortalIndex];
        while (true)
        {
            if (PortalMarks[cur] == PortalMark.SideWalker)
            {
                break;
            }
            else if (PortalMarks[next] == PortalMark.SideWalker)
            {
                PortalSequence porSeq = new PortalSequence()
                {
                    PortalPtr = cur,
                    NextPortalPtrIndex = GetIndexOf(next)
                };
                PortalSequence.Add(porSeq);
                PortalMarks[cur] = PortalMark.SideWalker;
                break;
            }
            else if (PortalMarks[next] == PortalMark.MainWalker)
            {
                PortalSequence porSeq = new PortalSequence()
                {
                    PortalPtr = cur,
                    NextPortalPtrIndex = GetIndexOf(next)
                };
                PortalSequence.Add(porSeq);
                PortalMarks[cur] = PortalMark.SideWalker;
                LocalIndex1d empyIndex = GetNotCalculatedIndexOfPortalNode(PortalNodes[cur]);
                if (empyIndex.index != -1) { IntegrationStartIndicies.Add(empyIndex); }
                break;
            }
            else if (next == cur)
            {
                PortalSequence porSeq = new PortalSequence()
                {
                    PortalPtr = cur,
                    NextPortalPtrIndex = -1,
                };
                PortalSequence.Add(porSeq);
                PortalMarks[cur] = PortalMark.SideWalker;
                LocalIndex1d empyIndex = GetNotCalculatedIndexOfPortalNode(PortalNodes[cur]);
                if (empyIndex.index != -1) { IntegrationStartIndicies.Add(empyIndex); }
                break;
            }
            else
            {
                PortalSequence porSeq = new PortalSequence()
                {
                    PortalPtr = cur,
                    NextPortalPtrIndex = portalSequence.Length + 1,
                };
                PortalSequence.Add(porSeq);
                PortalMarks[cur] = PortalMark.SideWalker;
                LocalIndex1d empyIndex = GetNotCalculatedIndexOfPortalNode(PortalNodes[cur]);
                if(empyIndex.index != -1) { IntegrationStartIndicies.Add(empyIndex); }
                cur = next;
                next = ConnectionIndicies[next];
            }
        }
        int GetIndexOf(int portalIndex)
        {
            for (int i = 0; i < portalSequence.Length; i++)
            {
                if (portalSequence[i].PortalPtr == portalIndex)
                {
                    return i;
                }
            }
            return -1;
        }
    }
    void PickSectorsFromNewPortalSequences()
    {
        for (int i = _newPortalSequenceStartIndex; i < PortalSequence.Length; i++)
        {
            int portalIndex = PortalSequence[i].PortalPtr;
            int windowIndex = PortalNodes[portalIndex].WinPtr;
            WindowNode windowNode = WindowNodes[windowIndex];
            int winToSecCnt = windowNode.WinToSecCnt;
            int winToSecPtr = windowNode.WinToSecPtr;
            for (int j = 0; j < winToSecCnt; j++)
            {
                int secPtr = WinToSecPtrs[j + winToSecPtr];
                if (SectorMarks[secPtr] != 0) { continue; }
                SectorMarks[secPtr] = IntegrationField.Length;
                IntegrationField.Add(new IntegrationFieldSector(secPtr));
                FlowField.Add(new FlowFieldSector(secPtr));
            }
        }
    }
    LocalIndex1d GetNotCalculatedIndexOfPortalNode(PortalNode portalNode)
    {
        int2 portal1General2d = new int2(portalNode.Portal1.Index.C, portalNode.Portal1.Index.R);
        int2 portal2General2d = new int2(portalNode.Portal2.Index.C, portalNode.Portal2.Index.R);
        int2 portal1Sector2d = portal1General2d / SectorColAmount;
        int2 portal2Sector2d = portal2General2d / SectorColAmount;
        int portal1Sector1d = portal1Sector2d.y * SectorMatrixColAmount + portal1Sector2d.x;
        int portal2Sector1d = portal2Sector2d.y * SectorMatrixColAmount + portal2Sector2d.x;
        int2 portal1SectorStart2d = portal1Sector2d * SectorColAmount;
        int2 portal2SectorStart2d = portal2Sector2d * SectorColAmount;
        int2 portal1Local2d = portal1General2d - portal1SectorStart2d;
        int2 portal2Local2d = portal2General2d - portal2SectorStart2d;
        int portal1Local1d = portal1Local2d.y * SectorColAmount + portal1Local2d.x;
        int portal2Local1d = portal2Local2d.y * SectorColAmount + portal2Local2d.x;
        if (SectorMarks[portal1Sector1d] == 0 && SectorMarks[portal2Sector1d] != 0)
        {
            return new LocalIndex1d(portal1Local1d, portal1Sector1d);
        }
        else if (SectorMarks[portal2Sector1d] == 0 && SectorMarks[portal1Sector1d] != 0)
        {
            return new LocalIndex1d(portal2Local1d, portal2Sector1d);
        }
        return new LocalIndex1d(-1,-1);
    }
}
