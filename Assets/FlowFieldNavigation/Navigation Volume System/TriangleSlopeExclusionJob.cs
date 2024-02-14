using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

[BurstCompile]
internal struct TriangleSlopeExclusionJob : IJob
{
    const float THRESHOLD = 0.4f;
    [ReadOnly] internal NativeArray<int> Triangles;
    [ReadOnly] internal NativeArray<float3> Verticies;
    [WriteOnly] internal NativeList<int> SlopeExcludedTriangles;
    public void Execute()
    {
        for(int i = 0; i < Triangles.Length; i+=3)
        {
            int v1Index = Triangles[i];
            int v2Index = Triangles[i + 1];
            int v3Index = Triangles[i + 2];

            float3 v1 = Verticies[v1Index];
            float3 v2 = Verticies[v2Index];
            float3 v3 = Verticies[v3Index];
            float3 planeNormal = math.cross(v2 - v1, v3 - v1);
            float3 planeNormalNormalized = math.normalizesafe(planeNormal);
            if(math.dot(planeNormalNormalized, new float3(0,1,0)) < THRESHOLD)
            {
                SlopeExcludedTriangles.Add(v1Index);
                SlopeExcludedTriangles.Add(v2Index);
                SlopeExcludedTriangles.Add(v3Index);
            }
        }
    }
}