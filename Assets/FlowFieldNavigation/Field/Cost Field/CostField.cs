using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

internal class CostField
{
    internal int Offset;
    internal NativeArray<byte> Costs;
    internal NativeArray<byte> BaseCosts;
    internal NativeArray<uint> StampCounts;
    internal UnsafeListReadOnly<byte> CostsLReadonlyUnsafe;
    internal CostField(Walkability[][] walkabilityMatrix, int offset)
    {
        int fieldRowAmount = FlowFieldUtilities.FieldRowAmount;
        int fieldColAmount = FlowFieldUtilities.FieldColAmount;
        Offset = offset;

        //configure costs
        UnsafeList<byte> costsG = new UnsafeList<byte>(fieldRowAmount * fieldColAmount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        costsG.Length = fieldColAmount * fieldRowAmount;
        Costs = new NativeArray<byte>(fieldColAmount * fieldRowAmount, Allocator.Persistent);
        BaseCosts = new NativeArray<byte>(Costs.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        StampCounts = new NativeArray<uint>(Costs.Length, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        CostsLReadonlyUnsafe = FlowFieldUtilitiesUnsafe.ToUnsafeListRedonly(Costs);
        CalculateCosts();
        ConvertToNewCosts();
        BaseCosts.CopyFrom(Costs);

        //HELPERS
        void CalculateCosts()
        {
            //calculate costs without offset
            for (int r = 0; r < fieldRowAmount; r++)
            {
                for (int c = 0; c < fieldColAmount; c++)
                {
                    int index = r * fieldColAmount + c;
                    byte cost = walkabilityMatrix[r][c] == Walkability.Walkable ? (byte)1 : byte.MaxValue;
                    cost = c == 0 || c == fieldColAmount - 1 || r == 0 || r == fieldRowAmount - 1 ? byte.MaxValue : cost;
                    costsG[index] = cost;
                }
            }
            //apply offset
            for (int r = 0; r < fieldRowAmount; r++)
            {
                for (int c = 0; c < fieldColAmount; c++)
                {
                    if (walkabilityMatrix[r][c] == Walkability.Unwalkable)
                    {
                        Index2 index = new Index2(r, c);
                        int minX = index.C - Offset < 0 ? 0 : index.C - Offset;
                        int maxX = index.C + Offset > fieldColAmount - 1 ? fieldColAmount - 1 : index.C + Offset;
                        int minY = index.R - Offset < 0 ? 0 : index.R - Offset;
                        int maxY = index.R + Offset > fieldRowAmount - 1 ? fieldRowAmount - 1 : index.R + Offset;

                        for (int row = minY; row <= maxY; row++)
                        {
                            for (int col = minX; col <= maxX; col++)
                            {
                                int i = row * fieldColAmount + col;
                                costsG[i] = byte.MaxValue;
                            }
                        }
                    }
                }
            }
        }
        void ConvertToNewCosts()
        {
            int sectorTileAmount = FlowFieldUtilities.SectorTileAmount;
            int sectorColAmount = FlowFieldUtilities.SectorColAmount;
            int sectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount;
            //SET COSTS
            for (int y = 0; y < fieldRowAmount; y += sectorColAmount)
            {
                for(int x = 0; x < fieldColAmount; x += sectorColAmount)
                {
                    int2 sectorStart2d = new int2(x, y);
                    int2 sectorEnd2d = new int2(x + sectorColAmount, y + sectorColAmount);
                    int2 sector2d = new int2(x / sectorColAmount, y / sectorColAmount);
                    int sectorStart1d = sectorStart2d.y * fieldColAmount + sectorStart2d.x;
                    int sectorEnd1d = sectorEnd2d.y * fieldColAmount + sectorEnd2d.x;
                    int sector1d = sector2d.y * sectorMatrixColAmount + sector2d.x;
                    int sectorDownLeft = sectorStart1d;
                    int sectorUpLeft = sectorEnd1d - sectorColAmount;
                    NativeSlice<byte> curSector = new NativeSlice<byte>(Costs, sector1d * sectorTileAmount, sectorTileAmount);
                    for (int i = sectorDownLeft; i < sectorUpLeft; i += fieldColAmount)
                    {
                        for(int j = i; j < i + sectorColAmount; j++)
                        {
                            int curGeneral1d = j;
                            int2 curGeneral2d = new int2(j % fieldColAmount, j / fieldColAmount);
                            int2 curLocal2d = curGeneral2d - sectorStart2d;
                            int curLocal1d = curLocal2d.y * sectorColAmount + curLocal2d.x;
                            byte curCost = costsG[curGeneral1d];
                            curSector[curLocal1d] = curCost;
                        }
                    }
                }
            }
        }
    }
    internal void DisposeAll()
    {
        Costs.Dispose();
        BaseCosts.Dispose();
        StampCounts.Dispose();
    }
    
}