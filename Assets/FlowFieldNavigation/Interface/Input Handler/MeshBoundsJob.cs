using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

[BurstCompile]
internal struct MeshBoundsJob : IJob
{
    [ReadOnly] public NativeArray<float3> Verticies;
    [WriteOnly] public NativeReference<float2> MeshStartPos;
    [WriteOnly] public NativeReference<float2> MeshEndPos;
    public void Execute()
    {
        float2 mins = new float2(float.MaxValue, float.MaxValue);
        float2 maxs = new float2(float.MinValue, float.MinValue);
        for (int i = 0; i < Verticies.Length; i++)
        {
            float3 vertex = Verticies[i];
            float2 vert2 = new float2(vertex.x, vertex.z);
            mins = math.min(mins, vert2);
            maxs = math.max(maxs, vert2);
        }
        MeshStartPos.Value = mins;
        MeshEndPos.Value = maxs;
    }
}
