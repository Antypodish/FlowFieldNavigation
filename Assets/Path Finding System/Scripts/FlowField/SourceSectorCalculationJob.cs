using Unity.Jobs;
using Unity.Burst;
using System.IO;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Mathematics;

[BurstCompile]
public struct SourceSectorCalculationJob : IJob
{
    public float SectorSize;
    public int SectorColAmount;
    public int SectorTileAmount;
    public int SectorMatrixColAmount;
    [ReadOnly] public NativeSlice<float2> Sources;
    [ReadOnly] public UnsafeList<PathSectorState> SectorStateTable;
    [ReadOnly] public NativeArray<ActivePortal> PortalSequence;
    [ReadOnly] public UnsafeList<int> SectorToPickedTable;
    [ReadOnly] public NativeArray<UnsafeList<ActiveWaveFront>> ActiveWaveFrontListArray;
    [ReadOnly] public UnsafeList<PortalNode> PortalNodes;

    [WriteOnly] public NativeList<int> SectorFlowStartIndiciesToCalculateIntegration;
    [WriteOnly] public NativeList<int> SectorFlowStartIndiciesToCalculateFlow;
    public void Execute()
    {
        for (int i = 0; i < Sources.Length; i++)
        {
            float2 pos = Sources[i];
            int sector1d = FlowFieldUtilities.PosToSector1D(pos, SectorSize, SectorMatrixColAmount);
            if (SectorStateTable[sector1d] != PathSectorState.IntegrationCalculated)
            {
                SectorStateTable[sector1d] = PathSectorState.IntegrationCalculated;
                int flowStartIndex = SectorToPickedTable[sector1d];
                SectorFlowStartIndiciesToCalculateIntegration.Add(flowStartIndex);
            }
            int pickedSectorIndex = (SectorToPickedTable[sector1d] - 1) / SectorTileAmount;
            UnsafeList<ActiveWaveFront> waveFronts = ActiveWaveFrontListArray[pickedSectorIndex];
            for (int j = 0; j < waveFronts.Length; j++)
            {
                ActiveWaveFront front = waveFronts[j];
                if (front.IsTarget()) { continue; }
                int portalSequenceCurIndex = front.PortalSequenceIndex;
                int portalSequenceNextIndex = front.PortalSequenceIndex + 1;
                ActivePortal curPortalSequenceNode = PortalSequence[portalSequenceCurIndex];
                ActivePortal nextPortalSequenceNode = PortalSequence[portalSequenceNextIndex];
                //if (nextPortalSequenceNode.IsTermintor()) { continue; }
                PortalNode curNode = PortalNodes[curPortalSequenceNode.Index];
                PortalNode nextNode = PortalNodes[nextPortalSequenceNode.Index];
                int commonSector = FlowFieldUtilities.GetCommonSector(curNode, nextNode, SectorColAmount, SectorMatrixColAmount);
                if (SectorStateTable[commonSector] == PathSectorState.IntegrationCalculated) { continue; }
                SectorStateTable[commonSector] = PathSectorState.IntegrationCalculated;
                pickedSectorIndex = (SectorToPickedTable[commonSector] - 1) / SectorTileAmount;
            }
        }
    }
}