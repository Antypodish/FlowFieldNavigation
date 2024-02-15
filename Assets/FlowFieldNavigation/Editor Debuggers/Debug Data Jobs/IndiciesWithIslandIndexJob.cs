using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace FlowFieldNavigation
{

    [BurstCompile]
    internal struct IndiciesWithIslandIndexJob : IJob
    {
        internal int FieldColAmount;
        internal int FieldRowAmount;
        internal int SectorColAmount;
        internal int SectorTileAmount;
        internal int SectorMatrixColAmount;
        [ReadOnly] internal NativeArray<byte> CostField;
        [ReadOnly] internal IslandFieldProcessor IslandFieldProcessor;
        [WriteOnly] internal NativeList<IndexIslandPair> IndiciesWithValidIslands;
        public void Execute()
        {
            for (int r = 0; r < FieldRowAmount; r++)
            {
                for (int c = 0; c < FieldColAmount; c++)
                {
                    int2 index = new int2(c, r);
                    int islandField = IslandFieldProcessor.GetIsland(index);
                    if (islandField == int.MaxValue) { continue; }
                    LocalIndex1d local = FlowFieldUtilities.GetLocal1D(index, SectorColAmount, SectorMatrixColAmount);
                    if (CostField[local.sector * SectorTileAmount + local.index] == byte.MaxValue) { continue; }
                    IndexIslandPair pair = new IndexIslandPair()
                    {
                        FieldIndex = index,
                        Island = islandField,
                    };
                    IndiciesWithValidIslands.Add(pair);
                }
            }
        }
    }
    internal struct IndexIslandPair
    {
        internal int2 FieldIndex;
        internal int Island;
    }

}