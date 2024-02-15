using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace FlowFieldNavigation
{

    [BurstCompile]
    internal struct HeightDifToCostField : IJob
    {
        internal float VoxVerSize;
        internal float MaxSurfaceHeightDifference;
        internal int XVoxCount;
        internal int ZVoxCount;
        [ReadOnly] internal NativeArray<HeightTile> HighestVoxelTable;
        [WriteOnly] internal NativeArray<byte> Costs;
        public void Execute()
        {
            for (int i = 0; i < HighestVoxelTable.Length; i++)
            {
                int cur = i;
                int n = i + XVoxCount;
                int e = i + 1;
                int s = i - XVoxCount;
                int w = i - 1;
                int ne = n + 1;
                int se = s + 1;
                int sw = s - 1;
                int nw = n - 1;

                //OVERFLOWS
                bool nOverflow = n >= HighestVoxelTable.Length;
                bool eOverflow = (e % XVoxCount) == 0;
                bool sOverflow = s < 0;
                bool wOverflow = (cur % XVoxCount) == 0;

                n = math.select(n, cur, nOverflow);
                e = math.select(e, cur, eOverflow);
                s = math.select(s, cur, sOverflow);
                w = math.select(w, cur, wOverflow);
                ne = math.select(ne, cur, nOverflow || eOverflow);
                se = math.select(se, cur, sOverflow || eOverflow);
                sw = math.select(sw, cur, sOverflow || wOverflow);
                nw = math.select(nw, cur, nOverflow || wOverflow);

                HeightTile curHeightTile = HighestVoxelTable[cur];
                HeightTile nHeightTile = HighestVoxelTable[n];
                HeightTile eHeightTile = HighestVoxelTable[e];
                HeightTile sHeightTile = HighestVoxelTable[s];
                HeightTile wHeightTile = HighestVoxelTable[w];
                HeightTile neHeightTile = HighestVoxelTable[ne];
                HeightTile seHeightTile = HighestVoxelTable[se];
                HeightTile swHeightTile = HighestVoxelTable[sw];
                HeightTile nwHeightTile = HighestVoxelTable[nw];

                float curHeight = (curHeightTile.VoxIndex.y - curHeightTile.StackCount + 1) * VoxVerSize;
                float nHeight = nHeightTile.VoxIndex.y * VoxVerSize;
                float eHeight = eHeightTile.VoxIndex.y * VoxVerSize;
                float sHeight = sHeightTile.VoxIndex.y * VoxVerSize;
                float wHeight = wHeightTile.VoxIndex.y * VoxVerSize;
                float neHeight = neHeightTile.VoxIndex.y * VoxVerSize;
                float seHeight = seHeightTile.VoxIndex.y * VoxVerSize;
                float swHeight = swHeightTile.VoxIndex.y * VoxVerSize;
                float nwHeight = nwHeightTile.VoxIndex.y * VoxVerSize;

                float4 y1 = new float4(nHeight, eHeight, sHeight, wHeight);
                float4 y2 = new float4(neHeight, seHeight, swHeight, nwHeight);

                float4 yDif1 = curHeight - y1;
                float4 yDif2 = curHeight - y2;

                bool4 muchDifferent1 = yDif1 > MaxSurfaceHeightDifference;
                bool4 muchDifferent2 = yDif2 > MaxSurfaceHeightDifference;
                bool4 muchDifResult = muchDifferent1 | muchDifferent2;

                if (muchDifResult.x | muchDifResult.y | muchDifResult.z | muchDifResult.w) { Costs[i] = byte.MaxValue; }
            }
        }
    }
    internal struct HeightTile
    {
        internal int3 VoxIndex;
        internal int StackCount;
    }

}