using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace FlowFieldNavigation
{

    [BurstCompile]
    internal struct LOSTransferJob : IJob
    {
        internal int2 Target;
        internal int LOSRange;
        internal int SectorColAmount;
        internal int SectorMatrixColAmount;
        internal int SectorMatrixRowAmount;
        internal int SectorTileAmount;
        internal int PathIndex;

        [ReadOnly] internal NativeArray<IntegrationTile> IntegrationField;
        [ReadOnly] internal PathSectorToFlowStartMapper FlowStartMap;
        [ReadOnly] internal NativeArray<int> SectorFlowStartTable;
        
        internal NativeArray<bool> LosArray;
        public void Execute()
        {
            int2 targetSector2d = FlowFieldUtilities.GetSector2D(Target, SectorColAmount);
            int extensionLength = LOSRange / SectorColAmount + math.select(0, 1, LOSRange % SectorColAmount > 0);
            int2 rangeTopRightSector = targetSector2d + new int2(extensionLength, extensionLength);
            int2 rangeBotLeftSector = targetSector2d - new int2(extensionLength, extensionLength);
            rangeTopRightSector = new int2()
            {
                x = math.select(rangeTopRightSector.x, SectorMatrixColAmount - 1, rangeTopRightSector.x >= SectorMatrixColAmount),
                y = math.select(rangeTopRightSector.y, SectorMatrixRowAmount - 1, rangeTopRightSector.y >= SectorMatrixRowAmount)
            };
            rangeBotLeftSector = new int2()
            {
                x = math.select(rangeBotLeftSector.x, 0, rangeBotLeftSector.x < 0),
                y = math.select(rangeBotLeftSector.y, 0, rangeBotLeftSector.y < 0)
            };

            for (int row = rangeBotLeftSector.y; row <= rangeTopRightSector.y; row++)
            {
                for (int col = rangeBotLeftSector.x; col <= rangeTopRightSector.x; col++)
                {
                    int sector1d = row * SectorMatrixColAmount + col;
                    if(FlowStartMap.TryGet(PathIndex, sector1d, out int newSectorFlowStart))
                    {
                        NativeSlice<bool> toSlice = new NativeSlice<bool>(LosArray, newSectorFlowStart, SectorTileAmount);
                        int sectorIntegrationStart = SectorFlowStartTable[sector1d];
                        NativeSlice<IntegrationTile> fromSlice = new NativeSlice<IntegrationTile>(IntegrationField, sectorIntegrationStart, SectorTileAmount);
                        Transfer(toSlice, fromSlice);
                    }
                }
            }
        }
        void Transfer(NativeSlice<bool> toSlice, NativeSlice<IntegrationTile> fromSlice)
        {
            for(int i = 0; i < fromSlice.Length; i++)
            {
                toSlice[i] = (fromSlice[i].Mark & IntegrationMark.LOSPass) == IntegrationMark.LOSPass;
            }
        }
    }


}