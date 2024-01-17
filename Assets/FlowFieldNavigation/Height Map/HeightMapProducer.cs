using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using System.Diagnostics;

public class HeightMapProducer
{
    public NativeList<float3> Verticies;
    public NativeList<int> Triangles;
    public NativeArray<TileTriangleSpan> TileTrianglePointerSpans;
    public NativeList<int> TileTrianglePointers;
    public HeightMapProducer()
    {
        Triangles = new NativeList<int>(Allocator.Persistent);
        TileTrianglePointerSpans = new NativeArray<TileTriangleSpan>(FlowFieldUtilities.FieldTileAmount, Allocator.Persistent);
        TileTrianglePointers = new NativeList<int>(Allocator.Persistent);
    }
    public void GenerateHeightMap(Mesh[] meshes, Transform[] meshParentTransforms)
    {
        //Merge and copy data to native containers
        NativeList<float3> verticies = new NativeList<float3>(Allocator.Persistent);
        NativeList<int> triangles = new NativeList<int>(Allocator.Persistent);
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
                verticies.Add(position + math.rotate(rotation, meshVerticies[j] * scale));
            }
            for (int j = 0; j < meshTriangles.Length; j++)
            {
                triangles.Add(vertexStart + meshTriangles[j]);
            }
            vertexStart += meshVerticies.Length;

        }
        Verticies = verticies;

        HeightMapGenerationJob heightMapJob = new HeightMapGenerationJob()
        {
            UpDirection = new float3(0, 1f, 0f),
            InputTriangles = triangles,
            InputVertecies = verticies,
            OutputTriangles = Triangles,
        };
        heightMapJob.Schedule().Complete();

        TileHeightSubmissionJob tileHeightSubmission = new TileHeightSubmissionJob()
        {
            TileSize = FlowFieldUtilities.TileSize,
            FieldColAmount = FlowFieldUtilities.FieldColAmount,
            FieldRowAmount = FlowFieldUtilities.FieldRowAmount,
            Verticies = Verticies,
            Triangles = Triangles,
            TileTrianglePointers = TileTrianglePointers,
            TileTrianglePointerSpans = TileTrianglePointerSpans,
        };
        tileHeightSubmission.Schedule().Complete();
    }

}