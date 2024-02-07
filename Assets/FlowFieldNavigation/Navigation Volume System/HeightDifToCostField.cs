using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

[BurstCompile]
internal struct HeightDifToCostField : IJob
{
    internal int XVoxCount;
    internal int ZVoxCount;
    [ReadOnly] internal NativeArray<HeightTile> HighestVoxelTable;
    [WriteOnly] internal NativeArray<byte> Costs;
    public void Execute()
    {
        for(int i = 0; i < HighestVoxelTable.Length; i++)
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

            int curHeight = curHeightTile.VoxIndex.y - curHeightTile.StackCount + 1;
            int nHeight = nHeightTile.VoxIndex.y;
            int eHeight = eHeightTile.VoxIndex.y;
            int sHeight = sHeightTile.VoxIndex.y;
            int wHeight = wHeightTile.VoxIndex.y;
            int neHeight = neHeightTile.VoxIndex.y;
            int seHeight = seHeightTile.VoxIndex.y;
            int swHeight = swHeightTile.VoxIndex.y;
            int nwHeight = nwHeightTile.VoxIndex.y;

            int4 y1 = new int4(nHeight, eHeight, sHeight, wHeight);
            int4 y2 = new int4(neHeight, seHeight, swHeight, nwHeight);

            int4 yDif1 = curHeight - y1;
            int4 yDif2 = curHeight - y2;

            bool4 muchDifferent1 = yDif1 > 1;
            bool4 muchDifferent2 = yDif2 > 1;
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