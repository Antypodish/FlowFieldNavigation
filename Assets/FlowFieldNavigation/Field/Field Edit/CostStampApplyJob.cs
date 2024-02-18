using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;


namespace FlowFieldNavigation
{
    [BurstCompile]
    internal struct CostStampApplyJob : IJob
    {
        internal int Offset;
        internal NativeList<CostEdit> NewCostEdits;
        internal NativeArray<byte> Costs;
        internal NativeArray<byte> BaseCosts;
        internal NativeArray<uint> CostStamps;
        internal int FieldColAmount;
        internal int FieldRowAmount;
        internal int SectorColAmount;
        internal int SectorMatrixColAmount;
        public void Execute()
        {
            //GIVE OFFSET TO OBSTACLE REQUESTS
            for (int i = 0; i < NewCostEdits.Length; i++)
            {
                CostEdit edit = NewCostEdits[i];
                int2 newBotLeft = edit.BotLeftBound + new int2(-Offset, -Offset);
                int2 newTopRight = edit.TopRightBound + new int2(Offset, Offset);

                newBotLeft.x = math.select(newBotLeft.x, 0, newBotLeft.x < 0);
                newBotLeft.y = math.select(newBotLeft.y, 0, newBotLeft.y < 0);
                newTopRight.x = math.select(newTopRight.x, FieldColAmount - 1, newTopRight.x >= FieldColAmount);
                newTopRight.y = math.select(newTopRight.y, FieldRowAmount - 1, newTopRight.y >= FieldRowAmount);
                NewCostEdits[i] = new CostEdit()
                {
                    TopRightBound = newTopRight,
                    BotLeftBound = newBotLeft,
                    EditType = edit.EditType,
                };
            }
            ApplyCostUpdate();

        }
        void ApplyCostUpdate()
        {
            int sectorColAmount = SectorColAmount;
            int sectorMatrixColAmount = SectorMatrixColAmount;
            int sectorTileAmount = SectorColAmount * SectorColAmount;

            for (int i = 0; i < NewCostEdits.Length; i++)
            {
                CostEdit edit = NewCostEdits[i];
                Index2 botLeft = new Index2(edit.BotLeftBound.y, edit.BotLeftBound.x);
                Index2 topRight = new Index2(edit.TopRightBound.y, edit.TopRightBound.x);
                if (botLeft.R == 0) { botLeft.R += 1; }
                if (botLeft.C == 0) { botLeft.C += 1; }
                if (topRight.R == FieldRowAmount - 1) { topRight.R -= 1; }
                if (topRight.C == FieldColAmount - 1) { topRight.C -= 1; }

                int eastCount = topRight.C - botLeft.C;
                int northCount = topRight.R - botLeft.R;
                LocalIndex1d localBotLeft = GetLocalIndex(botLeft);
                LocalIndex1d startLocal1d = localBotLeft;
                LocalIndex1d curLocalIndex = localBotLeft;
                NativeSlice<byte> costSector;
                NativeSlice<uint> costStampSector;
                NativeSlice<byte> baseCostSector;
                if (edit.EditType == CostEditType.Set)
                {
                    for (int index = 0; index <= northCount; index++)
                    {
                        for (int j = 0; j <= eastCount; j++)
                        {
                            costSector = new NativeSlice<byte>(Costs, curLocalIndex.sector * sectorTileAmount, sectorTileAmount);
                            costStampSector = new NativeSlice<uint>(CostStamps, curLocalIndex.sector * sectorTileAmount, sectorTileAmount);
                            costStampSector[curLocalIndex.index] += 1;
                            costSector[curLocalIndex.index] = byte.MaxValue;
                            curLocalIndex = GetEast(curLocalIndex);
                        }
                        startLocal1d = GetNorth(startLocal1d);
                        curLocalIndex = startLocal1d;
                    }
                }
                else if (edit.EditType == CostEditType.Clear)
                {

                    for (int index = 0; index <= northCount; index++)
                    {
                        for (int j = 0; j <= eastCount; j++)
                        {
                            costSector = new NativeSlice<byte>(Costs, curLocalIndex.sector * sectorTileAmount, sectorTileAmount);
                            costStampSector = new NativeSlice<uint>(CostStamps, curLocalIndex.sector * sectorTileAmount, sectorTileAmount);
                            baseCostSector = new NativeSlice<byte>(BaseCosts, curLocalIndex.sector * sectorTileAmount, sectorTileAmount);

                            uint curStamp = costStampSector[curLocalIndex.index] - 1;
                            costStampSector[curLocalIndex.index] = curStamp;
                            if (curStamp == 0)
                            {
                                costSector[curLocalIndex.index] = baseCostSector[curLocalIndex.index];
                            }
                            curLocalIndex = GetEast(curLocalIndex);
                        }
                        startLocal1d = GetNorth(startLocal1d);
                        curLocalIndex = startLocal1d;
                    }
                }
            }




            LocalIndex1d GetLocalIndex(Index2 index)
            {
                int2 general2d = new int2(index.C, index.R);
                int2 sector2d = general2d / sectorColAmount;
                int sector1d = sector2d.y * sectorMatrixColAmount + sector2d.x;
                int2 sectorStart2d = sector2d * sectorColAmount;
                int2 local2d = general2d - sectorStart2d;
                int local1d = local2d.y * sectorColAmount + local2d.x;
                return new LocalIndex1d(local1d, sector1d);
            }
            LocalIndex1d GetEast(LocalIndex1d local)
            {
                int eLocal1d = local.index + 1;
                bool eLocalOverflow = (eLocal1d % sectorColAmount) == 0;
                int eSector1d = math.select(local.sector, local.sector + 1, eLocalOverflow);
                eLocal1d = math.select(eLocal1d, local.index - sectorColAmount + 1, eLocalOverflow);
                return new LocalIndex1d(eLocal1d, eSector1d);
            }
            LocalIndex1d GetNorth(LocalIndex1d local)
            {
                int nLocal1d = local.index + sectorColAmount;
                bool nLocalOverflow = nLocal1d >= sectorTileAmount;
                int nSector1d = math.select(local.sector, local.sector + sectorMatrixColAmount, nLocalOverflow);
                nLocal1d = math.select(nLocal1d, local.index - (sectorColAmount * sectorColAmount - sectorColAmount), nLocalOverflow);
                return new LocalIndex1d(nLocal1d, nSector1d);
            }
        }
    }

}