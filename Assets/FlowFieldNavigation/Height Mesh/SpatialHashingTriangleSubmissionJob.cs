using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;

[BurstCompile]
public struct SpatialHashingTriangleSubmissionJob : IJob
{
    public float FieldVerticalSize;
    public float FieldHorizontalSize;
    public float BaseSpatialGridSize;
    [ReadOnly] public NativeArray<float3> Verticies;
    [ReadOnly] public NativeArray<int> Triangles;

    public NativeList<int> NewTriangles;
    public NativeList<UnsafeList<HashTile>> SpatialHashGrids;
    public NativeHashMap<float, int> TileSizeToGridIndex;

    public void Execute()
    {
        SetHashTileStartIndicies();
        SubmitTriangles();
    }
    void SetHashTileStartIndicies()
    {
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
            float2 boxPos = (mins + maxs) / 2;
            float2 sizes = maxs - mins;
            float size = math.max(sizes.x, sizes.y);
            int hashGridSizeMultiplier = (int)math.floor(size / BaseSpatialGridSize);
            float tileSize = hashGridSizeMultiplier * BaseSpatialGridSize + BaseSpatialGridSize;
            if (TileSizeToGridIndex.TryGetValue(tileSize, out int gridIndex))
            {
                UnsafeList<HashTile> hashGrid = SpatialHashGrids[gridIndex];
                int2 cellIndex = FlowFieldUtilities.PosTo2D(boxPos, tileSize);
                int gridColAmount = (int)math.ceil(FieldHorizontalSize / tileSize);
                int cellIndex1d = cellIndex.y * gridColAmount + cellIndex.x;
                HashTile cell = hashGrid[cellIndex1d];
                cell.Length+=3;
                hashGrid[cellIndex1d] = cell;
            }
        }
        int currentStartIndex = 0;
        for(int i = 0; i < SpatialHashGrids.Length; i++)
        {
            UnsafeList<HashTile> grid = SpatialHashGrids[i];
            for(int j = 0; j < grid.Length; j++)
            {
                HashTile tile = grid[j];
                tile.Start = currentStartIndex;
                currentStartIndex += tile.Length;
                tile.Length = 0;
                grid[j] = tile;
            }
        }
        NewTriangles.Length = currentStartIndex;
    }
    void SubmitTriangles()
    {
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
            float2 boxPos = (mins + maxs) / 2;
            float2 sizes = maxs - mins;
            float size = math.max(sizes.x, sizes.y);
            int hashGridSizeMultiplier = (int)math.floor(size / BaseSpatialGridSize);
            float tileSize = hashGridSizeMultiplier * BaseSpatialGridSize + BaseSpatialGridSize;
            if (TileSizeToGridIndex.TryGetValue(tileSize, out int gridIndex))
            {
                UnsafeList<HashTile> hashGrid = SpatialHashGrids[gridIndex];
                int2 cellIndex = FlowFieldUtilities.PosTo2D(boxPos, tileSize);
                int gridColAmount = (int)math.ceil(FieldHorizontalSize / tileSize);
                int cellIndex1d = cellIndex.y * gridColAmount + cellIndex.x;
                HashTile cell = hashGrid[cellIndex1d];
                int firstTriangleVertexIndex = cell.Start + cell.Length;
                cell.Length+=3;
                hashGrid[cellIndex1d] = cell;

                NewTriangles[firstTriangleVertexIndex] = v1Index;
                NewTriangles[firstTriangleVertexIndex + 1] = v2Index;
                NewTriangles[firstTriangleVertexIndex + 2] = v3Index;
            }
        }

    }
}
