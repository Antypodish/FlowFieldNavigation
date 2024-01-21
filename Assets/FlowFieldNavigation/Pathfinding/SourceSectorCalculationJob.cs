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
    public int SectorMatrixRowAmount;
    public int LOSRange;
    public int2 TargetIndex;
    [ReadOnly] public NativeSlice<float2> Sources;
    [ReadOnly] public NativeArray<ActivePortal> PortalSequence;
    [ReadOnly] public UnsafeList<int> SectorToPickedTable;
    [ReadOnly] public NativeArray<int> PickedToSectorTable;
    [ReadOnly] public NativeArray<UnsafeList<ActiveWaveFront>> ActiveWaveFrontListArray;
    [ReadOnly] internal NativeArray<PortalNode> PortalNodes;

    public NativeList<int> SectorFlowStartIndiciesToCalculateIntegration;
    public NativeList<int> SectorFlowStartIndiciesToCalculateFlow;

    public UnsafeList<PathSectorState> SectorStateTable;
    public NativeArray<SectorsWihinLOSArgument> SectorWithinLOSState;
    public void Execute()
    {
        SectorFlowStartIndiciesToCalculateFlow.Clear();
        SectorFlowStartIndiciesToCalculateIntegration.Clear();


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
                    if ((SectorStateTable[commonSector] & PathSectorState.IntegrationCalculated) == PathSectorState.IntegrationCalculated) { continue; }
                    SectorStateTable[commonSector] |= PathSectorState.IntegrationCalculated;
                    SectorFlowStartIndiciesToCalculateIntegration.Add(SectorToPickedTable[commonSector]);
                }
            }
        }

        if (ContainsSectorsWithinLOSRange(SectorFlowStartIndiciesToCalculateIntegration))
        {
            SectorsWihinLOSArgument argument = SectorWithinLOSState[0];
            argument |= SectorsWihinLOSArgument.RequestedSectorWithinLOS;
            SectorWithinLOSState[0] = argument;
        }
    }
    bool ContainsSectorsWithinLOSRange(NativeArray<int> integrationRequestedSectors)
    {
        int losRange = LOSRange;
        int sectorColAmount = SectorColAmount;
        int sectorMatrixColAmount = SectorMatrixColAmount;
        int sectorMatrixRowAmount = SectorMatrixRowAmount;
        int sectorTileAmount = SectorTileAmount;

        int2 targetSector2d = FlowFieldUtilities.GetSector2D(TargetIndex, sectorColAmount);
        int extensionLength = losRange / sectorColAmount + math.select(0, 1, losRange % sectorColAmount > 0);
        int2 rangeTopRightSector = targetSector2d + new int2(extensionLength, extensionLength);
        int2 rangeBotLeftSector = targetSector2d - new int2(extensionLength, extensionLength);
        rangeTopRightSector = new int2()
        {
            x = math.select(rangeTopRightSector.x, sectorMatrixColAmount - 1, rangeTopRightSector.x >= sectorMatrixColAmount),
            y = math.select(rangeTopRightSector.y, sectorMatrixRowAmount - 1, rangeTopRightSector.y >= sectorMatrixRowAmount)
        };
        rangeBotLeftSector = new int2()
        {
            x = math.select(rangeBotLeftSector.x, 0, rangeBotLeftSector.x < 0),
            y = math.select(rangeBotLeftSector.y, 0, rangeBotLeftSector.y < 0)
        };
        for (int i = 0; i < integrationRequestedSectors.Length; i++)
        {
            int sectorFlowStart = integrationRequestedSectors[i];
            int sector1d = PickedToSectorTable[(sectorFlowStart - 1) / sectorTileAmount];
            int sectorCol = sector1d % sectorMatrixColAmount;
            int sectorRow = sector1d / sectorMatrixColAmount;

            bool withinColRange = sectorCol >= rangeBotLeftSector.x && sectorCol <= rangeTopRightSector.x;
            bool withinRowRange = sectorRow >= rangeBotLeftSector.y && sectorRow <= rangeTopRightSector.y;
            if (withinColRange && withinRowRange) { return true; }
        }
        return false;
    }
}