using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

[BurstCompile]
internal struct HeightDifToCostField : IJob
{
    internal int XVoxCount;
    internal int ZVoxCount;
    [ReadOnly] internal NativeArray<int3> HighestVoxelTable;
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

            int3 curVoxel = HighestVoxelTable[cur];
            int3 nVoxel = HighestVoxelTable[n];
            int3 eVoxel = HighestVoxelTable[e];
            int3 sVoxel = HighestVoxelTable[s];
            int3 wVoxel = HighestVoxelTable[w];
            int3 neVoxel = HighestVoxelTable[ne];
            int3 seVoxel = HighestVoxelTable[se];
            int3 swVoxel = HighestVoxelTable[sw];
            int3 nwVoxel = HighestVoxelTable[nw];

            int4 y1 = new int4(nVoxel.y, eVoxel.y, sVoxel.y, wVoxel.y);
            int4 y2 = new int4(neVoxel.y, seVoxel.y, swVoxel.y, nwVoxel.y);

            int4 yDif1 = curVoxel.y - y1;
            int4 yDif2 = curVoxel.y - y2;

            bool4 muchDifferent1 = yDif1 > 5;
            bool4 muchDifferent2 = yDif2 > 5;
            bool4 muchDifResult = muchDifferent1 & muchDifferent2;
            if(muchDifResult.x | muchDifResult.y | muchDifResult.z | muchDifResult.w) { Costs[i] = byte.MaxValue; }
        }
    }
}
