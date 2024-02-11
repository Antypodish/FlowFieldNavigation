using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;

[BurstCompile]
internal struct TriangleSpatialHashingTileSizeCalculationJob : IJob
{
    internal float BaseSpatialGridSize;
    [ReadOnly] internal NativeArray<float3> Verticies;
    [ReadOnly] internal NativeArray<int> Triangles;
    internal NativeList<float> GridTileSizes;
    public void Execute()
    {
        NativeHashSet<float> tileSizeSet = new NativeHashSet<float>(0, Allocator.Temp);
        for (int i = 0; i < Triangles.Length; i += 3)
        {
            int v1Index = Triangles[i];
            int v2Index = Triangles[i + 1];
            int v3Index = Triangles[i + 2];
            float3 v13d = Verticies[v1Index];
            float3 v23d = Verticies[v2Index];
            float3 v33d = Verticies[v3Index];
            float2 v1 = new float2(v13d.x, v13d.z);
            float2 v2 = new float2(v23d.x, v23d.z);
            float2 v3 = new float2(v33d.x, v33d.z);
            float2 mins = math.min(math.min(v1, v2), v3);
            float2 maxs = math.max(math.max(v1, v2), v3);
            float2 sizes = maxs - mins;
            float size = math.max(sizes.x, sizes.y);
            int hashGridSizeMultiplier = (int)math.floor(size / BaseSpatialGridSize);
            float tileSize = hashGridSizeMultiplier * BaseSpatialGridSize + BaseSpatialGridSize;
            tileSizeSet.Add(tileSize);
        }
        NativeArray<float> tileSizes = tileSizeSet.ToNativeArray(Allocator.Temp);
        for(int i = 0; i < tileSizes.Length; i++)
        {
            GridTileSizes.Add(tileSizes[i]);
        }
    }
}
