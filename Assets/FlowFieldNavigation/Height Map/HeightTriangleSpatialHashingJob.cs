using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

public struct HeightTriangleSpatialHashingJob : IJob
{
    [ReadOnly] public NativeArray<float3> Verticies;
    [ReadOnly] public NativeArray<int> Triangles;

    public NativeArray<int> SpatialHashedTriangleStartIndicies;
    public void Execute()
    {

    }
}
