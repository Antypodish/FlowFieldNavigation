using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
internal struct SectorMeshBuildJob : IJob
{
    internal float SectorSize;
    internal int MeshRowCount;
    internal int MeshColCount;

    internal float2 MeshStartPos;
    [ReadOnly] internal TriangleSpatialHashGrid TriangleSpatialHashGrid;
    [ReadOnly] internal NativeArray<float3> HeightMeshVerts;
    [WriteOnly] internal NativeArray<Vector3> Verts;
    [WriteOnly] internal NativeArray<int> Trigs;
    public void Execute()
    {
        int vertIndex = 0;
        for (int r = 0; r < MeshRowCount; r++)
        {
            for (int c = 0; c < MeshColCount; c++)
            {
                float2 v2d = MeshStartPos + new float2(c * SectorSize, r * SectorSize);
                float3 v3d = new float3(v2d.x, GetHeight(v2d) + 0.1f, v2d.y);
                Verts[vertIndex++] = v3d;
            }
        }

        int trigIndex = 0;
        for (int r = 0; r < MeshRowCount - 1; r++)
        {
            for (int c = 0; c < MeshColCount - 1; c++)
            {
                int vCur = r * MeshColCount + c;
                int vUp = vCur + MeshColCount;
                int vRight = vCur + 1;
                Trigs[trigIndex++] = vCur;
                Trigs[trigIndex++] = vUp;
                Trigs[trigIndex++] = vCur;
                Trigs[trigIndex++] = vRight;
            }
        }
    }
    float GetHeight(float2 pos)
    {
        int unique = 0;
        float curHeight = float.MinValue;
        for (int i = 0; i < TriangleSpatialHashGrid.GetGridCount(); i++)
        {
            bool succesfull = TriangleSpatialHashGrid.TryGetIterator(pos, i, out TriangleSpatialHashGridIterator triangleGridIterator);
            if (!succesfull) { return 0; }
            while (triangleGridIterator.HasNext())
            {
                NativeSlice<int> triangles = triangleGridIterator.GetNextRow();
                unique++;
                for (int j = 0; j < triangles.Length; j += 3)
                {
                    int v1Index = triangles[j];
                    int v2Index = triangles[j + 1];
                    int v3Index = triangles[j + 2];
                    float3 v13d = HeightMeshVerts[v1Index];
                    float3 v23d = HeightMeshVerts[v2Index];
                    float3 v33d = HeightMeshVerts[v3Index];
                    float2 v1 = new float2(v13d.x, v13d.z);
                    float2 v2 = new float2(v23d.x, v23d.z);
                    float2 v3 = new float2(v33d.x, v33d.z);

                    BarycentricCoordinates barCords = GetBarycentricCoordinatesForEachVectorInTheOrderUVW(v1, v2, v3, pos);
                    if (barCords.u < 0 || barCords.w < 0 || barCords.v < 0) { continue; }
                    float newHeight = v13d.y * barCords.u + v23d.y * barCords.v + v33d.y * barCords.w;
                    curHeight = math.select(curHeight, newHeight, newHeight > curHeight);
                }
            }
        }
        return math.select(curHeight,0, curHeight == float.MinValue);
    }
    BarycentricCoordinates GetBarycentricCoordinatesForEachVectorInTheOrderUVW(float2 a, float2 b, float2 c, float2 p)
    {
        float2 v0 = b - a, v1 = c - a, v2 = p - a;
        float d00 = math.dot(v0, v0);
        float d01 = math.dot(v0, v1);
        float d11 = math.dot(v1, v1);
        float d20 = math.dot(v2, v0);
        float d21 = math.dot(v2, v1);
        float denom = d00 * d11 - d01 * d01;
        float v = (d11 * d20 - d01 * d21) / denom;
        float w = (d00 * d21 - d01 * d20) / denom;
        float u = 1.0f - v - w;
        return new BarycentricCoordinates()
        {
            v = v,
            u = u,
            w = w,
        };
    }
}