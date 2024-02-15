using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;
using System.IO;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;


namespace FlowFieldNavigation
{
    [BurstCompile]
    internal struct LOSCleanJob : IJob
    {
        internal int2 Target;
        internal int LOSRange;
        internal int SectorColAmount;
        internal int SectorMatrixColAmount;
        internal int SectorMatrixRowAmount;
        internal int SectorTileAmount;

        internal NativeArray<IntegrationTile> IntegrationField;
        [ReadOnly] internal UnsafeList<int> SectorToPickedTable;
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
                    int sectorIntegrationStart = SectorToPickedTable[sector1d];
                    if (sectorIntegrationStart == 0) { continue; }

                    for (int i = sectorIntegrationStart; i < sectorIntegrationStart + SectorTileAmount; i++)
                    {
                        IntegrationTile tile = IntegrationField[i];
                        IntegrationField[i] = new IntegrationTile()
                        {
                            Cost = tile.Cost,
                            Mark = ~((~tile.Mark) | IntegrationMark.LOSBlock | IntegrationMark.LOSC | IntegrationMark.LOSPass),
                        };
                    }
                }
            }
        }
    }

}