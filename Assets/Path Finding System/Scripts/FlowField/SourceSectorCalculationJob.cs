using Unity.Jobs;
using Unity.Burst;
using System.IO;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.XR;

[BurstCompile]
public struct SourceSectorCalculationJob : IJob
{
    public float SectorSize;
    public int SectorColAmount;
    public int SectorTileAmount;
    public int SectorMatrixColAmount;
    public int2 TargetIndex;
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
        int targetSector1d = FlowFieldUtilities.GetSector1D(TargetIndex, SectorColAmount, SectorMatrixColAmount);
        for (int i = 0; i < Sources.Length; i++)
        {
            float2 pos = Sources[i];
            int sector1d = FlowFieldUtilities.PosToSector1D(pos, SectorSize, SectorMatrixColAmount);
            if ((SectorStateTable[sector1d] & PathSectorState.IntegrationCalculated) != PathSectorState.IntegrationCalculated)
            {
                SectorStateTable[sector1d] |= PathSectorState.IntegrationCalculated;
                int flowStartIndex = SectorToPickedTable[sector1d];
                SectorFlowStartIndiciesToCalculateIntegration.Add(flowStartIndex);
            }
            if ((SectorStateTable[sector1d] & PathSectorState.FlowCalculated) != PathSectorState.FlowCalculated)
            {
                SectorStateTable[sector1d] |= PathSectorState.FlowCalculated;
                int flowStartIndex = SectorToPickedTable[sector1d];
                SectorFlowStartIndiciesToCalculateFlow.Add(flowStartIndex);
            }
            int pickedSectorIndex = (SectorToPickedTable[sector1d] - 1) / SectorTileAmount;
            UnsafeList<ActiveWaveFront> waveFronts = ActiveWaveFrontListArray[pickedSectorIndex];
            for (int j = 0; j < waveFronts.Length; j++)
            {
                ActiveWaveFront front = waveFronts[j];
                if (front.IsTarget()){ continue; }
                int portalSequenceCurIndex = front.PortalSequenceIndex;
                ActivePortal curPortalSequenceNode = PortalSequence[portalSequenceCurIndex];
                int portalSequenceNextIndex = curPortalSequenceNode.NextIndex;
                if(portalSequenceNextIndex == -1)
                {
                    int commonSector = targetSector1d;
                    if ((SectorStateTable[commonSector] & PathSectorState.IntegrationCalculated) == PathSectorState.IntegrationCalculated) { continue; }
                    SectorStateTable[commonSector] |= PathSectorState.IntegrationCalculated;
                    SectorFlowStartIndiciesToCalculateIntegration.Add(SectorToPickedTable[commonSector]);
                }
                else
                {
                    ActivePortal nextPortalSequenceNode = PortalSequence[portalSequenceNextIndex];
                    PortalNode curNode = PortalNodes[curPortalSequenceNode.Index];
                    PortalNode nextNode = PortalNodes[nextPortalSequenceNode.Index];
                    int commonSector = FlowFieldUtilities.GetCommonSector(curNode, nextNode, SectorColAmount, SectorMatrixColAmount);
                    if ((SectorStateTable[commonSector] & PathSectorState.IntegrationCalculated) == PathSectorState.IntegrationCalculated) { continue; }
                    SectorStateTable[commonSector] |= PathSectorState.IntegrationCalculated;
                    SectorFlowStartIndiciesToCalculateIntegration.Add(SectorToPickedTable[commonSector]);
                }
            }
        }
    }
}