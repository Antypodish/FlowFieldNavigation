using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;

[BurstCompile]
internal struct TriangleNormalTestJob : IJob
{
    internal float3 UpDirection;
    [ReadOnly] internal NativeArray<float3> InputVertecies;
    [ReadOnly] internal NativeArray<int> InputTriangles;

    internal NativeList<int> OutputTriangles;
    internal NativeList<float3> OutputVerticies;

    public void Execute()
    {
        OutputVerticies.CopyFrom(InputVertecies);
        for (int i = 0; i < InputTriangles.Length; i += 3)
        {
            int vertIndex1 = InputTriangles[i];
            int vertIndex2 = InputTriangles[i + 1];
            int vertIndex3 = InputTriangles[i + 2];

            float3 vertex1 = InputVertecies[vertIndex1];
            float3 vertex2 = InputVertecies[vertIndex2];
            float3 vertex3 = InputVertecies[vertIndex3];

            float3 normal = math.cross(vertex3 - vertex2, vertex1 - vertex2);
            if(math.dot(normal, UpDirection) > 0)
            {
                OutputTriangles.Add(vertIndex1);
                OutputTriangles.Add(vertIndex2);
                OutputTriangles.Add(vertIndex3);
            }
        }
    }
}
