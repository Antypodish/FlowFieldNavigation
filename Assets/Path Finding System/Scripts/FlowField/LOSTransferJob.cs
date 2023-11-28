using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct LOSTransferJob : IJob
{
    public int2 Target;
    public int LOSRange;
    public int SectorColAmount;
    public int SectorMatrixColAmount;
    public int SectorMatrixRowAmount;
    public int SectorTileAmount;

    [ReadOnly] public NativeArray<IntegrationTile> IntegrationField;
    [ReadOnly] public UnsafeList<int> SectorToPickedTable;
    public UnsafeLOSBitmap LOSBitmap;
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

                Transfer(sectorIntegrationStart);
            }
        }
    }

    public void Transfer(int integrationStartIndex)
    {
        int lastIndexOfSector = integrationStartIndex + SectorTileAmount - 1;

        //HANDLE STARTING BITS
        int startByteIndex = LOSBitmap.GetByteIndex(integrationStartIndex);
        int startBitRank = LOSBitmap.GetBitRank(integrationStartIndex);

        IntegrationTile startTile1 = IntegrationField[integrationStartIndex];
        IntegrationTile startTile2 = IntegrationField[integrationStartIndex + 1];
        IntegrationTile startTile3 = IntegrationField[integrationStartIndex + 2];
        IntegrationTile startTile4 = IntegrationField[integrationStartIndex + 3];
        IntegrationTile startTile5 = IntegrationField[integrationStartIndex + 4];
        IntegrationTile startTile6 = IntegrationField[integrationStartIndex + 5];
        IntegrationTile startTile7 = IntegrationField[integrationStartIndex + 6];
        IntegrationTile startTile8 = IntegrationField[integrationStartIndex + 7];
        bool startTile1Los = (startTile1.Mark & IntegrationMark.LOSPass) == IntegrationMark.LOSPass;
        bool startTile2Los = (startTile2.Mark & IntegrationMark.LOSPass) == IntegrationMark.LOSPass;
        bool startTile3Los = (startTile3.Mark & IntegrationMark.LOSPass) == IntegrationMark.LOSPass;
        bool startTile4Los = (startTile4.Mark & IntegrationMark.LOSPass) == IntegrationMark.LOSPass;
        bool startTile5Los = (startTile5.Mark & IntegrationMark.LOSPass) == IntegrationMark.LOSPass;
        bool startTile6Los = (startTile6.Mark & IntegrationMark.LOSPass) == IntegrationMark.LOSPass;
        bool startTile7Los = (startTile7.Mark & IntegrationMark.LOSPass) == IntegrationMark.LOSPass;
        bool startTile8Los = (startTile8.Mark & IntegrationMark.LOSPass) == IntegrationMark.LOSPass;
        int startTile1Mask = math.select(0, 1, startTile1Los);
        int startTile2Mask = math.select(0, 2, startTile2Los);
        int startTile3Mask = math.select(0, 4, startTile3Los);
        int startTile4Mask = math.select(0, 8, startTile4Los);
        int startTile5Mask = math.select(0, 16, startTile5Los);
        int startTile6Mask = math.select(0, 32, startTile6Los);
        int startTile7Mask = math.select(0, 64, startTile7Los);
        int startTile8Mask = math.select(0, 128, startTile8Los);
        int startMask = startTile1Mask | startTile2Mask | startTile3Mask | startTile4Mask | startTile5Mask | startTile6Mask | startTile7Mask | startTile8Mask;
        LOSBitmap.SetBitsOfByteStartingFrom(startByteIndex, startBitRank, (byte) startMask);

        int start = integrationStartIndex + 8 - startBitRank;
        int end = integrationStartIndex + SectorTileAmount - 8;

        //HANDLE MIDDLE BITS
        int i;
        for(i = start; i < end; i += 8)
        {
            IntegrationTile tile1 = IntegrationField[integrationStartIndex];
            IntegrationTile tile2 = IntegrationField[integrationStartIndex + 1];
            IntegrationTile tile3 = IntegrationField[integrationStartIndex + 2];
            IntegrationTile tile4 = IntegrationField[integrationStartIndex + 3];
            IntegrationTile tile5 = IntegrationField[integrationStartIndex + 4];
            IntegrationTile tile6 = IntegrationField[integrationStartIndex + 5];
            IntegrationTile tile7 = IntegrationField[integrationStartIndex + 6];
            IntegrationTile tile8 = IntegrationField[integrationStartIndex + 7];
            bool tile1Los = (tile1.Mark & IntegrationMark.LOSPass) == IntegrationMark.LOSPass;
            bool tile2Los = (tile2.Mark & IntegrationMark.LOSPass) == IntegrationMark.LOSPass;
            bool tile3Los = (tile3.Mark & IntegrationMark.LOSPass) == IntegrationMark.LOSPass;
            bool tile4Los = (tile4.Mark & IntegrationMark.LOSPass) == IntegrationMark.LOSPass;
            bool tile5Los = (tile5.Mark & IntegrationMark.LOSPass) == IntegrationMark.LOSPass;
            bool tile6Los = (tile6.Mark & IntegrationMark.LOSPass) == IntegrationMark.LOSPass;
            bool tile7Los = (tile7.Mark & IntegrationMark.LOSPass) == IntegrationMark.LOSPass;
            bool tile8Los = (tile8.Mark & IntegrationMark.LOSPass) == IntegrationMark.LOSPass;
            int tile1Mask = math.select(0, 1, tile1Los);
            int tile2Mask = math.select(0, 2, tile2Los);
            int tile3Mask = math.select(0, 4, tile3Los);
            int tile4Mask = math.select(0, 8, tile4Los);
            int tile5Mask = math.select(0, 16, tile5Los);
            int tile6Mask = math.select(0, 32, tile6Los);
            int tile7Mask = math.select(0, 64, tile7Los);
            int tile8Mask = math.select(0, 128, tile8Los);
            int mask = tile1Mask | tile2Mask | tile3Mask | tile4Mask | tile5Mask | tile6Mask | tile7Mask | tile8Mask;
            LOSBitmap.SetByteOfBit(i, (byte)mask);
        }

        int lastByteIndex = LOSBitmap.GetByteIndex(i);
        int lastBitRank = LOSBitmap.GetBitRank(integrationStartIndex + SectorTileAmount - 1);

        IntegrationTile lastTile1 = IntegrationField[math.select(i, 0, i > lastIndexOfSector)];
        IntegrationTile lastTile2 = IntegrationField[math.select(i + 1, 0, i + 1 > lastIndexOfSector)];
        IntegrationTile lastTile3 = IntegrationField[math.select(i + 2, 0, i + 2 > lastIndexOfSector)];
        IntegrationTile lastTile4 = IntegrationField[math.select(i + 3, 0, i + 3 > lastIndexOfSector)];
        IntegrationTile lastTile5 = IntegrationField[math.select(i + 4, 0, i + 4 > lastIndexOfSector)];
        IntegrationTile lastTile6 = IntegrationField[math.select(i + 5, 0, i + 5 > lastIndexOfSector)];
        IntegrationTile lastTile7 = IntegrationField[math.select(i + 6, 0, i + 6 > lastIndexOfSector)];
        IntegrationTile lastTile8 = IntegrationField[math.select(i + 7, 0, i + 7 > lastIndexOfSector)];
        bool lastTile1Los = (lastTile1.Mark & IntegrationMark.LOSPass) == IntegrationMark.LOSPass;
        bool lastTile2Los = (lastTile2.Mark & IntegrationMark.LOSPass) == IntegrationMark.LOSPass;
        bool lastTile3Los = (lastTile3.Mark & IntegrationMark.LOSPass) == IntegrationMark.LOSPass;
        bool lastTile4Los = (lastTile4.Mark & IntegrationMark.LOSPass) == IntegrationMark.LOSPass;
        bool lastTile5Los = (lastTile5.Mark & IntegrationMark.LOSPass) == IntegrationMark.LOSPass;
        bool lastTile6Los = (lastTile6.Mark & IntegrationMark.LOSPass) == IntegrationMark.LOSPass;
        bool lastTile7Los = (lastTile7.Mark & IntegrationMark.LOSPass) == IntegrationMark.LOSPass;
        bool lastTile8Los = (lastTile8.Mark & IntegrationMark.LOSPass) == IntegrationMark.LOSPass;
        int lastTile1Mask = math.select(0, 1, lastTile1Los);
        int lastTile2Mask = math.select(0, 2, lastTile2Los);
        int lastTile3Mask = math.select(0, 4, lastTile3Los);
        int lastTile4Mask = math.select(0, 8, lastTile4Los);
        int lastTile5Mask = math.select(0, 16, lastTile5Los);
        int lastTile6Mask = math.select(0, 32, lastTile6Los);
        int lastTile7Mask = math.select(0, 64, lastTile7Los);
        int lastTile8Mask = math.select(0, 128, lastTile8Los);
        int lastMask = lastTile1Mask | lastTile2Mask | lastTile3Mask | lastTile4Mask | lastTile5Mask | lastTile6Mask | lastTile7Mask | lastTile8Mask;
        LOSBitmap.SetBitsOfByteUntil(lastByteIndex, lastBitRank, (byte)lastMask);
    }
}
