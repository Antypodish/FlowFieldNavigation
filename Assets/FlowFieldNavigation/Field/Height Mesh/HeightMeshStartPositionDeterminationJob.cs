using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

[BurstCompile]
internal struct HeightMeshStartPositionDeterminationJob : IJob
{
    [ReadOnly] public NativeArray<float3> Verticies;
    [WriteOnly] public NativeReference<float2> BaseTranslationOut;
    public void Execute()
    {
        float minX = float.MaxValue;
        float minY = float.MaxValue;
        for(int i = 0; i < Verticies.Length; i++)
        {
            float3 vertex = Verticies[i];
            minX = math.min(vertex.x, minX);
            minY = math.min(vertex.z, minY);
        }
        BaseTranslationOut.Value = new float2(minX, minY);
    }
}
