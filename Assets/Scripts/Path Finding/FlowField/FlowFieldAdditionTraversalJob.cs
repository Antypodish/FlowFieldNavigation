using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using UnityEngine;
using Unity.Jobs;

[BurstCompile]
public struct FlowFieldAdditionTraversalJob : IJob
{
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
    public NativeList<int> ConnectionWindowIndicies;
    public NativeArray<int> SectorMarks;
    public NativeList<IntegrationFieldSector> IntegrationField;
    public NativeList<FlowFieldSector> FlowField;
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
            if (PortalMarks[next] == PortalMark.SideWalker)
            {
                PortalSequence porSeq = new PortalSequence()
                {
                    PortalPtr = cur,
                    NextPortalPtrIndex = GetIndexOf(next)
                };
                PortalSequence.Add(porSeq);
                PortalMarks[cur] = PortalMark.MainWalker;
                break;
            }
            if (PortalMarks[next] == PortalMark.MainWalker)
            {
                PortalSequence porSeq = new PortalSequence()
                {
                    PortalPtr = cur,
                    NextPortalPtrIndex = GetIndexOf(next)
                };
                PortalSequence.Add(porSeq);
                PortalMarks[cur] = PortalMark.MainWalker;
                PortalNode curNode = PortalNodes[cur];
                ConnectionWindowIndicies.Add(curNode.WinPtr);
                break;
            }
            if (next == cur)
            {
                PortalSequence porSeq = new PortalSequence()
                {
                    PortalPtr = cur,
                    NextPortalPtrIndex = -1,
                };
                PortalSequence.Add(porSeq);
                PortalMarks[cur] = PortalMark.MainWalker;
                PortalNode curNode = PortalNodes[cur];
                ConnectionWindowIndicies.Add(curNode.WinPtr);
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
                PortalMarks[cur] = PortalMark.MainWalker;
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
}
