using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;
using System.Diagnostics;

public class HeightMeshProducer
{
    public NativeList<float3> Verticies;
    public NativeList<int> Triangles;
    NativeList<UnsafeList<HashTile>> SpatialHashGrids;
    NativeHashMap<float, int> TileSizeToGridIndex;
    NativeHashMap<int, float> GridIndexToTileSize;
    public HeightMeshProducer()
    {
        Verticies = new NativeList<float3>(Allocator.Persistent);
        Triangles = new NativeList<int>(Allocator.Persistent);
        SpatialHashGrids = new NativeList<UnsafeList<HashTile>>(Allocator.Persistent);
        TileSizeToGridIndex = new NativeHashMap<float, int>(0, Allocator.Persistent);
        GridIndexToTileSize = new NativeHashMap<int, float>(0, Allocator.Persistent);
    }
    public void GenerateHeightMap(Mesh[] meshes, Transform[] meshParentTransforms)
    {
        //Merge and copy data to native containers
        NativeList<float3> tempVericies = new NativeList<float3>(Allocator.TempJob);
        NativeList<int> tempTriangles = new NativeList<int>(Allocator.TempJob);
        int vertexStart = 0;
        for (int i = 0; i < meshes.Length; i++)
        {
            Vector3[] meshVerticies = meshes[i].vertices;
            int[] meshTriangles = meshes[i].triangles;

            Transform meshTransform = meshParentTransforms[i];
            float3 position = meshTransform.position;
            float3 scale = meshTransform.localScale;
            quaternion rotation = meshTransform.rotation;

            for (int j = 0; j < meshVerticies.Length; j++)
            {
                tempVericies.Add(position + math.rotate(rotation, meshVerticies[j] * scale));
            }
            for (int j = 0; j < meshTriangles.Length; j++)
            {
                tempTriangles.Add(vertexStart + meshTriangles[j]);
            }
            vertexStart += meshVerticies.Length;

        }

        //Eliminate wrong normals
        TriangleNormalTestJob heightMapJob = new TriangleNormalTestJob()
        {
            UpDirection = new float3(0, 1f, 0f),
            InputTriangles = tempTriangles,
            InputVertecies = tempVericies,
            OutputTriangles = Triangles,
            OutputVerticies = Verticies,
        };
        heightMapJob.Schedule().Complete();
        tempVericies.Dispose();
        tempTriangles.Dispose();

        //Get grid tile sizes
        NativeList<float> gridTileSizes = new NativeList<float>(Allocator.TempJob);
        TriangleSpatialHashingTileSizeCalculationJob spatialHashingTileSizeCalculation = new TriangleSpatialHashingTileSizeCalculationJob()
        {
            BaseSpatialGridSize = FlowFieldUtilities.BaseTriangleSpatialGridSize,
            Triangles = Triangles,
            Verticies = Verticies,
            GridTileSizes = gridTileSizes,
        };
        spatialHashingTileSizeCalculation.Schedule().Complete();

        //Create grid according to grid tile sizes
        CreateHashGrids(gridTileSizes.AsArray().AsReadOnly());
        gridTileSizes.Dispose();

        //Submit triangles to spatial hashing
        NativeList<int> newTriangles = new NativeList<int>(Allocator.Persistent);
        SpatialHashingTriangleSubmissionJob spatialHashingTriangleSubmission = new SpatialHashingTriangleSubmissionJob()
        {
            BaseSpatialGridSize = FlowFieldUtilities.BaseTriangleSpatialGridSize,
            FieldHorizontalSize = FlowFieldUtilities.TileSize * FlowFieldUtilities.FieldColAmount,
            FieldVerticalSize = FlowFieldUtilities.TileSize * FlowFieldUtilities.FieldRowAmount,
            SpatialHashGrids = SpatialHashGrids,
            TileSizeToGridIndex = TileSizeToGridIndex,
            Triangles = Triangles,
            Verticies = Verticies,
            NewTriangles = newTriangles,
        };
        spatialHashingTriangleSubmission.Schedule().Complete();
        Triangles.Dispose();
        Triangles = newTriangles;
    }
    public TriangleSpatialHashGrid GetTriangleSpatialHashGrid()
    {
        return new TriangleSpatialHashGrid()
        {
            BaseSpatialGridSize = FlowFieldUtilities.BaseTriangleSpatialGridSize,
            FieldHorizontalSize = FlowFieldUtilities.FieldColAmount * FlowFieldUtilities.TileSize,
            FieldVerticalSize = FlowFieldUtilities.FieldRowAmount * FlowFieldUtilities.TileSize,
            GridIndexToTileSize = GridIndexToTileSize,
            HashedTriangles = Triangles,
            TriangleHashGrids = SpatialHashGrids,
        };
    }
    void CreateHashGrids(NativeArray<float>.ReadOnly gridTileSizes)
    {
        float fieldMinXIncluding = FlowFieldUtilities.FieldMinXIncluding;
        float fieldMinYIncluding = FlowFieldUtilities.FieldMinYIncluding;
        float fieldMaxXExcluding = FlowFieldUtilities.FieldMaxXExcluding;
        float fieldMaxYExcluding = FlowFieldUtilities.FieldMaxYExcluding;
        float fieldHorizontalSize = fieldMaxXExcluding - fieldMinXIncluding;
        float fieldVerticalSize = fieldMaxYExcluding - fieldMinYIncluding;
        for (int i = 0; i < gridTileSizes.Length; i++)
        {
            float tileSize = gridTileSizes[i];
            int rowAmount = (int)math.ceil(fieldVerticalSize / tileSize);
            int colAmount = (int)math.ceil(fieldHorizontalSize / tileSize);
            UnsafeList<HashTile> hashTiles = new UnsafeList<HashTile>(rowAmount * colAmount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            hashTiles.Length = rowAmount * colAmount;
            SpatialHashGrids.Add(hashTiles);
            TileSizeToGridIndex.Add(tileSize, SpatialHashGrids.Length - 1);
            GridIndexToTileSize.Add(SpatialHashGrids.Length - 1, tileSize);
        }
    }

}