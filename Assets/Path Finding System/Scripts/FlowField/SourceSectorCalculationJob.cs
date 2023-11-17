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
    [ReadOnly] public NativeArray<ActivePortal> PortalSequence;
    [ReadOnly] public UnsafeList<int> SectorToPickedTable;
    [ReadOnly] public NativeArray<UnsafeList<ActiveWaveFront>> ActiveWaveFrontListArray;
    [ReadOnly] public UnsafeList<PortalNode> PortalNodes;

    [WriteOnly] public NativeList<int> SectorFlowStartIndiciesToCalculateIntegration;
    [WriteOnly] public NativeList<int> SectorFlowStartIndiciesToCalculateFlow;

    public UnsafeList<PathSectorState> SectorStateTable;
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
                    FlowFieldUtilities.GetSectors(curNode, SectorColAmount, SectorMatrixColAmount, out int curSec1, out int curSec2);
                    FlowFieldUtilities.GetSectors(nextNode, SectorColAmount, SectorMatrixColAmount, out int nextSec1, out int nextSec2);

                    bool curSec1Common = curSec1 == nextSec1 || curSec1 == nextSec2;
                    bool curSec2Common = curSec2 == nextSec1 || curSec2 == nextSec2;
                    if (!curSec1Common && !curSec2Common) { continue; }
                    if(curSec1Common && curSec2Common)
                    {
                        if ((SectorStateTable[curSec1] & PathSectorState.IntegrationCalculated) != PathSectorState.IntegrationCalculated)
                        {
                            SectorStateTable[curSec1] |= PathSectorState.IntegrationCalculated;
                            SectorFlowStartIndiciesToCalculateIntegration.Add(SectorToPickedTable[curSec1]);
                        }
                        if ((SectorStateTable[curSec2] & PathSectorState.IntegrationCalculated) != PathSectorState.IntegrationCalculated)
                        {
                            SectorStateTable[curSec2] |= PathSectorState.IntegrationCalculated;
                            SectorFlowStartIndiciesToCalculateIntegration.Add(SectorToPickedTable[curSec2]);
                        }
                        continue;
                    }
                    int commonSector = curSec1Common ? curSec1 : curSec2;
                    SectorStateTable[commonSector] |= PathSectorState.IntegrationCalculated;
                    SectorFlowStartIndiciesToCalculateIntegration.Add(SectorToPickedTable[commonSector]);
                }
            }
        }
    }
}