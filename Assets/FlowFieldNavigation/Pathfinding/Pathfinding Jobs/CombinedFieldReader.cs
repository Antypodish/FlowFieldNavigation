using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace FlowFieldNavigation
{

    internal struct CombinedDynamicAreaFieldReader
    {
        [ReadOnly] internal NativeArray<SectorFlowStart> SectorFlowStartIndicies;
        [WriteOnly] internal NativeArray<FlowData> FlowField;
        [ReadOnly] internal NativeArray<IntegrationTile> IntegrationField;

        internal CombinedDynamicAreaFieldReader(NativeArray<SectorFlowStart> sectorFlowStartIndicies, NativeArray<FlowData> flowField, NativeArray<IntegrationTile> ıntegrationField)
        {
            SectorFlowStartIndicies = sectorFlowStartIndicies;
            FlowField = flowField;
            IntegrationField = ıntegrationField;
        }

        internal IntegrationTile GetIntegrationTileUnsafe(int sectorFlowStart, int local1d)
        {
            return IntegrationField[sectorFlowStart + local1d];
        }
        internal IntegrationTile GetIntegrationTile(int integrationFieldIndex)
        {
            return IntegrationField[integrationFieldIndex];
        }
        internal void SetFlow(int flowFieldIndex, FlowData flow)
        {
            FlowField[flowFieldIndex] = flow;
        }
        internal int FieldIndexToSectorIndex(int flowFieldOrIntegrationFieldIndex, int sectorTileAmount)
        {
            int flowStartIndex = (flowFieldOrIntegrationFieldIndex - 1) / sectorTileAmount * sectorTileAmount + 1;
            int sector1d = 0;
            for (int i = 0; i < SectorFlowStartIndicies.Length; i++)
            {
                SectorFlowStart flowStart = SectorFlowStartIndicies[i];
                sector1d = math.select(sector1d, flowStart.SectorIndex, flowStart.FlowStartIndex == flowStartIndex);
            }
            return sector1d;
        }

        internal int9 GetSectorFlowStartInThePassedOrder(int sec1, int sec2 = 0, int sec3 = 0, int sec4 = 0, int sec5 = 0, int sec6 = 0, int sec7 = 0, int sec8 = 0, int sec9 = 0)
        {
            int sector1FlowStart = 0;
            int sector2FlowStart = 0;
            int sector3FlowStart = 0;
            int sector4FlowStart = 0;
            int sector5FlowStart = 0;
            int sector6FlowStart = 0;
            int sector7FlowStart = 0;
            int sector8FlowStart = 0;
            int sector9FlowStart = 0;
            for (int i = 0; i < SectorFlowStartIndicies.Length; i++)
            {
                SectorFlowStart flowStart = SectorFlowStartIndicies[i];
                sector1FlowStart = math.select(sector1FlowStart, flowStart.FlowStartIndex, flowStart.SectorIndex == sec1);
                sector2FlowStart = math.select(sector2FlowStart, flowStart.FlowStartIndex, flowStart.SectorIndex == sec2);
                sector3FlowStart = math.select(sector3FlowStart, flowStart.FlowStartIndex, flowStart.SectorIndex == sec3);
                sector4FlowStart = math.select(sector4FlowStart, flowStart.FlowStartIndex, flowStart.SectorIndex == sec4);
                sector5FlowStart = math.select(sector5FlowStart, flowStart.FlowStartIndex, flowStart.SectorIndex == sec5);
                sector6FlowStart = math.select(sector6FlowStart, flowStart.FlowStartIndex, flowStart.SectorIndex == sec6);
                sector7FlowStart = math.select(sector7FlowStart, flowStart.FlowStartIndex, flowStart.SectorIndex == sec7);
                sector8FlowStart = math.select(sector8FlowStart, flowStart.FlowStartIndex, flowStart.SectorIndex == sec8);
                sector9FlowStart = math.select(sector9FlowStart, flowStart.FlowStartIndex, flowStart.SectorIndex == sec9);
            }
            return new int9()
            {
                a = sector1FlowStart,
                b = sector2FlowStart,
                c = sector3FlowStart,
                d = sector4FlowStart,
                e = sector5FlowStart,
                f = sector6FlowStart,
                g = sector7FlowStart,
                h = sector8FlowStart,
                i = sector9FlowStart,
            };
        }
    }


}